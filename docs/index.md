---
layout: home

hero:
  name: DapperMatic
  text: .NET Database DDL Made Simple
  tagline: C# library and REST API for database schema management across SQL Server, MySQL, PostgreSQL, and SQLite
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: View on GitHub
      link: https://github.com/mjczone/dappermatic
    - theme: none
      text: Version VERSION_NUMBER

features:
  - icon: 🔷
    title: .NET 8+ Library
    details: Modern C# library targeting .NET 8.0 with comprehensive DDL operations for multiple database providers
  - icon: 🏗️
    title: Model-First Approach
    details: Define your database schema using intuitive C# models with Dm* prefixed classes
  - icon: 🗄️
    title: Multi-Provider Support
    details: Works seamlessly with SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite
  - icon: 🛡️
    title: Type-Safe Operations
    details: Strongly-typed C# API prevents runtime errors and improves developer experience
  - icon: 🧪
    title: Thoroughly Tested
    details: Comprehensive test suite ensures reliability across all supported providers
  - icon: 📦
    title: NuGet Package
    details: Easy installation via NuGet with minimal dependencies - just add to your .NET project
  - icon: 🤖
    title: AI/LLM Optimized Docs
    details: Comprehensive LLM-friendly documentation - llms.txt for quick reference, llms-full.txt for complete guide
    link: /llms
    linkText: View AI Docs
---

## Choose Your Development Path

DapperMatic offers two powerful ways to manage your database schema. Choose the approach that best fits your project:

<div class="vp-feature-grid">
  <div class="vp-feature-item">
    <div class="vp-feature-icon">🛠️</div>
    <h3>.NET Library Development</h3>
    <p>Use DapperMatic directly in your .NET applications for database schema management</p>
    <ul>
      <li><strong>Best for:</strong> Console apps, desktop apps, microservices, custom tooling</li>
      <li><strong>Package:</strong> MJCZone.DapperMatic</li>
      <li><strong>Usage:</strong> Direct IDbConnection extensions</li>
    </ul>
    <div class="vp-feature-actions">
      <a href="/guide/getting-started" class="vp-button vp-button-brand">Library Quick Start</a>
    </div>
  </div>

  <div class="vp-feature-item">
    <div class="vp-feature-icon">🌐</div>
    <h3>Web API Integration</h3>
    <p>Add REST endpoints to your ASP.NET Core applications for database management</p>
    <ul>
      <li><strong>Best for:</strong> Web applications, admin panels, database management tools</li>
      <li><strong>Package:</strong> MJCZone.DapperMatic.AspNetCore</li>
      <li><strong>Usage:</strong> HTTP REST API endpoints</li>
    </ul>
    <div class="vp-feature-actions">
      <a href="/guide/web-api/getting-started" class="vp-button vp-button-alt">Web API Quick Start</a>
    </div>
  </div>
</div>

<div style="text-align: center; margin-top: 2rem; padding-top: 2rem; border-top: 1px solid var(--vp-c-divider); color: var(--vp-c-text-2); font-size: 0.9em;">
  <p>Made with ❤️ by <a href="https://www.mjczone.com" target="_blank">MJCZone Inc.</a></p>
  <p>Released under the <a href="/guide/license">LGPL v3 License</a></p>
</div>
