# Repository Guidelines

- Use **4 spaces** for indentation in C# code.
- Favor **file-scoped namespaces** (`namespace MyNamespace;`).
- Prefer `var` for local variables when the type is clear.
- Keep braces on the same line as declarations.
- After changing any files, run `dotnet test` from the repository root.
- Commit messages use the format `scope: summary` (e.g. `feat: add logging`).
- Follow standard C# naming conventions:
  - Use `PascalCase` for class names, method names, properties, and public fields.
  - Use `camelCase` for local variables and method parameters.
  - Use `_camelCase` for private fields.
- Document public APIs with XML documentation comments (`///`).
