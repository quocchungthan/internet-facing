"""Dump a PostgreSQL database using an Npgsql connection string received on stdin."""

import os
import re
import subprocess
import sys


def postgres_environment(connection):
    fields = []
    position = 0
    while position < len(connection):
        if connection[position].isspace() or connection[position] == ";":
            position += 1
            continue
        match = re.match(r"([^=;]+)=", connection[position:])
        if not match:
            raise ValueError("Invalid Npgsql connection string.")
        key = match[1].strip().lower()
        position += match.end()
        while position < len(connection) and connection[position].isspace():
            position += 1
        if position < len(connection) and connection[position] in "\"'":
            quote = connection[position]
            position += 1
            value = ""
            while True:
                end = connection.find(quote, position)
                if end < 0:
                    raise ValueError("Unterminated quoted connection value.")
                value += connection[position:end]
                position = end + 1
                if position < len(connection) and connection[position] == quote:
                    value += quote
                    position += 1
                    continue
                break
            while position < len(connection) and connection[position].isspace():
                position += 1
            if position < len(connection) and connection[position] != ";":
                raise ValueError("Invalid text after quoted connection value.")
        else:
            end = connection.find(";", position)
            end = len(connection) if end < 0 else end
            value = connection[position:end].strip()
            position = end
        fields.append((key, value))

    aliases = {
        "host": "PGHOST", "server": "PGHOST", "port": "PGPORT",
        "database": "PGDATABASE", "initial catalog": "PGDATABASE",
        "username": "PGUSER", "user name": "PGUSER", "user id": "PGUSER", "userid": "PGUSER",
        "password": "PGPASSWORD", "pwd": "PGPASSWORD",
        "ssl mode": "PGSSLMODE", "sslmode": "PGSSLMODE",
        "timeout": "PGCONNECT_TIMEOUT", "connection timeout": "PGCONNECT_TIMEOUT",
    }
    if {key for key, _ in fields} - aliases.keys():
        raise ValueError("Unsupported Npgsql connection option; use host, port, database, username, password, SSL mode and timeout.")
    env = {"PGPORT": "5432", "PGCONNECT_TIMEOUT": "15", "PGSSLMODE": "prefer"}
    env.update({aliases[key]: value for key, value in fields})
    if any(not env.get(key) for key in ("PGHOST", "PGDATABASE", "PGUSER", "PGPASSWORD")):
        raise ValueError("Host, database, username and password are required.")
    if not env["PGPORT"].isdigit() or not 1 <= int(env["PGPORT"]) <= 65535:
        raise ValueError("Invalid PostgreSQL port.")
    if not env["PGCONNECT_TIMEOUT"].isdigit() or int(env["PGCONNECT_TIMEOUT"]) < 1:
        raise ValueError("Connection timeout must be positive.")
    mode = env["PGSSLMODE"].lower().replace(" ", "").replace("-", "")
    modes = {value.replace("-", ""): value for value in ("disable", "allow", "prefer", "require", "verify-ca", "verify-full")}
    if mode not in modes:
        raise ValueError("Unsupported PostgreSQL SSL mode.")
    env["PGSSLMODE"] = modes[mode]
    return env


def dump(connection, output):
    env = postgres_environment(connection)
    command = ["docker", "run", "--rm", "--network", "host"]
    for key in env:
        command.extend(["--env", key])
    command.extend(["postgres:18-alpine", "pg_dump", "--clean", "--if-exists", "--no-password"])
    # Do not inherit libpq settings from the host, or expose provider errors containing credentials.
    child_env = {key: value for key, value in os.environ.items() if not key.startswith("PG")}
    child_env.update(env)
    with open(output, "wb") as destination:
        result = subprocess.run(command, env=child_env, stdout=destination, stderr=subprocess.PIPE, check=False)
    if result.returncode:
        raise RuntimeError("PostgreSQL dump failed; check Docker, network access, credentials and server/client version.")


def main():
    try:
        dump(sys.stdin.read(), sys.argv[1])
    except (ValueError, RuntimeError, OSError) as error:
        # Validation/runtime messages are fixed text; OSError filenames may contain private values.
        message = "Cannot execute PostgreSQL backup client or write dump." if isinstance(error, OSError) else str(error)
        print(message, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
