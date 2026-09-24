#!/usr/bin/env python3
"""One-shot, write-only Azure public DNS updater for a dedicated A record.

Configuration is trusted. Each run replaces the record with the current public IPv4.
"""

import argparse
import email.utils
import ipaddress
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

MAX_RESPONSE = 65536
TIMEOUT = 20


class DdnsError(Exception):
    """Only static, safe-to-log messages belong here."""


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Http:
    def __init__(self):
        self.opener = urllib.request.build_opener(NoRedirect())

    def request(self, method, url, headers=None, body=None):
        if not url.startswith("https://"):
            raise DdnsError("HTTPS is required")

        try:
            request = urllib.request.Request(url, data=body, headers=headers or {}, method=method)
            try:
                response = self.opener.open(request, timeout=TIMEOUT)
            except urllib.error.HTTPError as error:
                response = error

            with response:
                # Do not read error bodies: they can contain credentials.
                data = response.read(MAX_RESPONSE + 1) if 200 <= response.code < 300 else b""
                if len(data) > MAX_RESPONSE:
                    raise DdnsError("HTTP response exceeds size limit")
                return response.code, dict(response.headers.items()), data
        except DdnsError:
            raise
        except Exception:
            raise DdnsError("HTTP transport failed; remote outcome may be unknown") from None


class State:
    def __init__(self, directory, clock=time.time):
        self.path = os.path.join(directory, "cooldown.json")
        self.clock = clock

    def gated(self):
        try:
            with open(self.path, encoding="utf-8") as stream:
                return json.load(stream)["until"] > self.clock()
        except FileNotFoundError:
            return False

    def defer(self, value):
        until = (self.clock() + int(value) if value.strip().isdigit()
                 else email.utils.parsedate_to_datetime(value).timestamp())
        temporary = self.path + ".tmp"
        with open(temporary, "w", encoding="utf-8") as stream:
            json.dump({"until": until}, stream)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, self.path)


def checked_request(http, state, method, url, headers=None, body=None, expected=(200,)):
    if state.gated():
        raise DdnsError("Request deferred by Retry-After cooldown")

    status, response_headers, data = http.request(method, url, headers, body)
    response_headers = {key.lower(): value for key, value in response_headers.items()}

    if "retry-after" in response_headers:
        state.defer(response_headers["retry-after"])

    if status not in expected:
        raise DdnsError(f"HTTP {status}; no retry attempted")

    return response_headers, data


def run(config, state, http):
    """Caller owns the state lock. No DNS read or conditional update is performed."""
    if state.gated():
        return "cooldown: no requests made"

    _, data = checked_request(http, state, "GET", config.get("publicIpUrl", "https://api.ipify.org"))
    address = ipaddress.IPv4Address(data.decode("ascii").strip())
    if not address.is_global or address.is_multicast or address.is_reserved:
        raise DdnsError("Discovery did not return a public IPv4 address")
    address = str(address)

    with open(config["clientSecretFile"], encoding="utf-8") as stream:
        secret = stream.read().rstrip("\r\n")

    oauth = urllib.parse.urlencode({
        "client_id": config["clientId"],
        "client_secret": secret,
        "grant_type": "client_credentials",
        "scope": "https://management.azure.com/.default",
    }).encode("ascii")
    _, data = checked_request(http, state, "POST",
                             f"https://login.microsoftonline.com/{config['tenantId']}/oauth2/v2.0/token",
                             {"Content-Type": "application/x-www-form-urlencoded"}, oauth)
    headers = {"Authorization": "Bearer " + json.loads(data)["access_token"]}

    subscription, group, zone, name = (
        urllib.parse.quote(config[key], safe="")
        for key in ("subscriptionId", "resourceGroup", "zoneName", "recordName")
    )
    url = (f"https://management.azure.com/subscriptions/{subscription}/resourceGroups/{group}"
           f"/providers/Microsoft.Network/dnsZones/{zone}/A/{name}?api-version=2018-05-01")

    headers["Content-Type"] = "application/json"
    body = json.dumps({
        "properties": {
            "TTL": 300,
            "ARecords": [{"ipv4Address": address}],
        },
    }).encode("utf-8")

    checked_request(http, state, "PUT", url, headers, body, expected=(200, 201))
    return "updated: Azure accepted the public IPv4 record"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True)
    parser.add_argument("--state-dir", required=True)
    args = parser.parse_args(argv)

    try:
        import fcntl

        os.umask(0o077)
        os.makedirs(args.state_dir, mode=0o700, exist_ok=True)

        with open(os.path.join(args.state_dir, "lock"), "a") as lock:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise DdnsError("Another updater is running") from None

            with open(args.config, encoding="utf-8") as stream:
                config = json.load(stream)
            print(run(config, State(args.state_dir), Http()))
        return 0
    except DdnsError as error:
        print("azure-dns-ddns: " + str(error), file=sys.stderr)
    except Exception:
        print("azure-dns-ddns: operation failed (details suppressed)", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
