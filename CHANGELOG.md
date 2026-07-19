# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Added per-page horizontal orientation through `[SpreadsheetPage("Page", isVertical: false)]`; vertical orientation remains the default.
- Horizontal source rows are fetched independently before transposition so mixed field types are preserved.

### Fixed
- Parse integral fields from Google numeric values such as `6.0` and `200.0` without rounding fractional values.

- Yandex documents support

## [2.0.0] - 19-07-2026
### Refactored
- Replaced CSV downloads and manual delimiter parsing with structured Google Visualization DataTable responses.
- Import spreadsheet pages as structured row and column arrays, preserving commas, quotes, and multiline strings.
- Handle omitted trailing cells and empty rows without shifting columns or indexing outside a row.

### Changed
- Imports require the spreadsheet to be public or available through link sharing.
- No Google Cloud project, API key, OAuth flow, or service account is required.
- Private spreadsheets are not supported.

## [1.0.0] - 15-12-2023
### Refactored
- Parsing logic

## [0.1.1] - 23-07-2023
### Added
- JSON and binary serialization support
### Refactored
- Import settings caching

## [0.1.0] - 07-05-2023
### Added
- Basic functionality
