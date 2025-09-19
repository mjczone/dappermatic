# MJCZone.DapperMatic Documentation

This directory contains the VitePress documentation for MJCZone.DapperMatic.

## Getting Started

1. Install dependencies:
   ```bash
   npm install
   ```

2. Generate API documentation:
   ```bash
   npm run generate-api
   ```
   
   This command generates API documentation from the compiled assemblies and their XML documentation files.

3. Start the development server:
   ```bash
   npm run dev
   ```

4. Build for production:
   ```bash
   npm run build
   ```

## Project Structure

- `/docs/` - Manual documentation pages
- `/api/` - Auto-generated API documentation (not tracked in git)
- `/public/` - Static assets
- `/.vitepress/` - VitePress configuration
- `/scripts/` - Build scripts including API documentation generator

## API Documentation

The API documentation is auto-generated from the compiled .NET assemblies and should not be edited manually. The `/api/` directory (except for `.gitkeep`) is excluded from version control.

To regenerate the API documentation after making changes to the source code:

1. Build the .NET project to generate updated assemblies and XML documentation
2. Run `npm run generate-api`
3. The documentation will be generated in the `/api/` directory

## Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run generate-api` - Generate API documentation from assemblies
- `npm run preview` - Preview production build locally