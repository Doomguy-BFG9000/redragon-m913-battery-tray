# Security

The utility runs as the current user, requests no elevation, performs no network access, and opens only matching local HID interfaces. Releases should be reproduced from source and checked against `SHA256SUMS.txt`.

Security reports should include the app version, Windows version, exact device VID/PID, and the smallest reproducible description. Use the public GitHub repository's **Security > Report a vulnerability** page, and do not publish unrelated personal logs or credentials.

Current community releases are not Authenticode-signed. Treat the GitHub release page and its published SHA-256 checksums as the distribution and integrity reference; a self-signed certificate is not presented as public trust.
