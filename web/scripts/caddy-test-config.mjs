let input = '';
for await (const chunk of process.stdin) input += chunk;
const config = JSON.parse(input);
for (const server of Object.values(config.apps.http.servers)) {
  // Preserve production routes while testing without public DNS or certificate issuance.
  server.listen = [':80'];
  server.automatic_https = { disable: true };
  delete server.tls_connection_policies;
}
process.stdout.write(JSON.stringify(config));
