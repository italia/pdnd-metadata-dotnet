# Contributing to Pdnd.Metadata

Thank you for considering contributing to **Pdnd.Metadata**! Every contribution helps make PDND interoperability easier for the .NET community.

## How to Contribute

### Reporting Bugs

- Open an [issue](https://github.com/engineering87/pdnd-metadata-dotnet/issues) with a clear title and description.
- Include the .NET version, OS, and steps to reproduce.
- If applicable, include a minimal code sample or test case.

### Suggesting Features

- Open an [issue](https://github.com/engineering87/pdnd-metadata-dotnet/issues) describing the use case and expected behavior.
- If you have a design proposal, include it in the issue description.

### Pull Requests

1. **Fork** the repository and create your branch from `main`.
2. **Add tests** for any new functionality.
3. **Ensure all tests pass** by running `dotnet test`.
4. **Follow the existing coding style** — the project uses `sealed` classes, `record` types, nullable reference types, and XML doc comments on all public APIs.
5. **Keep changes focused** — one feature or fix per PR.
6. **Update documentation** if your change affects public API or behavior.

### Development Setup

```bash
git clone https://github.com/engineering87/pdnd-metadata-dotnet.git
cd pdnd-metadata-dotnet
dotnet build src/pdnd-metadata-dotnet/pdnd-metadata-dotnet.slnx
dotnet test src/pdnd-metadata-dotnet/pdnd-metadata-dotnet.slnx
```

### Code Style

- Use `sealed` for classes that are not designed for inheritance.
- Prefer `record` types for immutable data.
- Enable nullable reference types.
- Add XML doc comments on all public members.
- Follow the existing naming conventions in `PdndMetadataKeys` for new canonical keys.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
