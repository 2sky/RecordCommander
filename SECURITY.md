# Security Policy

## Supported Versions
The latest stable version on NuGet is actively maintained with security fixes. Older versions are not supported.

## Reporting a Vulnerability
If you find a security vulnerability, please open an issue on GitHub or contact the maintainers privately.

## Security Considerations
- The project does not include input validation. Validate all data before use.
- Do not commit strong name key files (`*.snk`) as they pose a security risk, as noted in `.gitignore`.

