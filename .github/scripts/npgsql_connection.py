"""Turns the postgresql:// URL in $DATABASE_URL (Neon's form) into an Npgsql "Key=Value;…" connection string.

Used by the deploy pipelines to run the EF Core migration bundle, which only understands Npgsql's form. Mirrors
api/MyPoGoCompanion.Api/Configuration/DatabaseConnectionString.cs: query parameters become keys with "_" read as
a space (sslmode, channel_binding → "channel binding"), and Kerberos (GSS) encryption is turned off.
A value already in Npgsql's form is passed through. Prints the result; never prints the input on errors.

With --direct, a Neon pooled host ("ep-…-pooler.…") becomes its direct host ("ep-….…"): Neon recommends a direct
connection for migrations, since its pooler (transaction mode) doesn't keep the session locks they take.

    CONNECTION=$(python3 .github/scripts/npgsql_connection.py --direct)
"""

import os
import sys
from urllib.parse import unquote, urlsplit


def quote(value: str) -> str:
    if any(c in value for c in ';="\'') or value != value.strip():
        return '"' + value.replace('"', '""') + '"'
    return value


def convert(value: str, direct: bool = False) -> str:
    if not value.lower().startswith(("postgres://", "postgresql://")):
        return value
    url = urlsplit(value)
    if not url.hostname:
        raise ValueError("The database URL has no host.")
    host = url.hostname
    if direct:
        first, dot, rest = host.partition(".")
        if first.endswith("-pooler"):
            host = first.removesuffix("-pooler") + dot + rest
    parts = {"Host": host, "Port": str(url.port or 5432)}
    if url.path.strip("/"):
        parts["Database"] = unquote(url.path.lstrip("/"))
    if url.username:
        parts["Username"] = unquote(url.username)
    if url.password is not None:
        parts["Password"] = unquote(url.password)
    for parameter in filter(None, url.query.split("&")):
        key, _, setting = parameter.partition("=")
        parts[unquote(key).replace("_", " ")] = unquote(setting)
    if not any(k.lower().replace(" ", "") == "gssencryptionmode" for k in parts):
        parts["GSS Encryption Mode"] = "Disable"
    return ";".join(f"{k}={quote(v)}" for k, v in parts.items())


if __name__ == "__main__":
    source = os.environ.get("DATABASE_URL", "")
    if not source.strip():
        sys.exit("DATABASE_URL is empty.")
    try:
        print(convert(source, direct="--direct" in sys.argv[1:]))
    except ValueError as error:  # the message never includes the URL (it holds the password)
        sys.exit(str(error))
