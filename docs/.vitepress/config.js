import { defineConfig } from "vitepress";

export default defineConfig({
  title: "DapperMatic",
  description:
    "IDbConnection extension methods for DDL operations across multiple database providers",

  base: "/dappermatic/",
  ignoreDeadLinks: true,

  markdown: { theme: { light: "github-light", dark: "github-dark" } },

  themeConfig: {
    logo: "/favicon.ico",

    nav: [
      { text: "Guide", link: "/guide/getting-started" },
      { text: "API Reference", link: "/api/" },
      {
        text: "GitHub",
        link: "https://github.com/mjczone/dappermatic",
      },
    ],

    sidebar: {
      "/guide/": [
        {
          text: "Getting Started",
          collapsed: false,
          items: [
            { text: "Installation", link: "/guide/installation" },
            { text: "Providers", link: "/guide/providers" },
            { text: "Models", link: "/guide/models" },
            { text: "Data Annotations", link: "/guide/data-annotations" },
            { text: "Configuration", link: "/guide/configuration" },
          ],
        },
        {
          text: "Usage",
          collapsed: false,
          items: [
            { text: "Extension Methods", link: "/guide/extension-methods/" },
            {
              text: "General Methods",
              link: "/guide/extension-methods/general-methods",
            },
            {
              text: "Schema Methods",
              link: "/guide/extension-methods/schema-methods",
            },
            {
              text: "Table Methods",
              link: "/guide/extension-methods/table-methods",
            },
            {
              text: "Column Methods",
              link: "/guide/extension-methods/column-methods",
            },
            {
              text: "Primary Key Methods",
              link: "/guide/extension-methods/primary-key-constraint-methods",
            },
            {
              text: "Check Constraint Methods",
              link: "/guide/extension-methods/check-constraint-methods",
            },
            {
              text: "Default Constraint Methods",
              link: "/guide/extension-methods/default-constraint-methods",
            },
            {
              text: "Foreign Key Methods",
              link: "/guide/extension-methods/foreign-key-constraint-methods",
            },
            {
              text: "Unique Constraint Methods",
              link: "/guide/extension-methods/unique-constraint-methods",
            },
            {
              text: "Index Methods",
              link: "/guide/extension-methods/index-methods",
            },
            {
              text: "View Methods",
              link: "/guide/extension-methods/view-methods",
            },
            { text: "Testing", link: "/guide/testing" },
          ],
        },
        {
          text: "About",
          collapsed: true,
          items: [
            { text: "Credits", link: "/guide/credits" },
            { text: "Roadmap", link: "/guide/roadmap" },
            { text: "License", link: "/guide/license" },
          ],
        },
      ],
      "/api/": [
        {
          text: "API Reference",
          items: [
            {
              text: "Overview",
              link: "/api/",
            },
            {
              text: "MJCZone.DapperMatic",
              link: "/api/mjczone.dappermatic/",
              items: [
                {
                  text: "📦 Root",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic/",
                },
                {
                  text: "📦 / Providers",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.providers/",
                },
                {
                  text: "📦 / Providers.SqlServer",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.providers.sqlserver/",
                },
                {
                  text: "📦 / Providers.Sqlite",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.providers.sqlite/",
                },
                {
                  text: "📦 / Providers.PostgreSql",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.providers.postgresql/",
                },
                {
                  text: "📦 / Providers.MySql",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.providers.mysql/",
                },
                {
                  text: "📦 / Providers.Base",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.providers.base/",
                },
                {
                  text: "📦 / Models",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.models/",
                },
                {
                  text: "📦 / Interfaces",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.interfaces/",
                },
                {
                  text: "📦 / DataAnnotations",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.dataannotations/",
                },
                {
                  text: "📦 / Converters",
                  link: "/api/mjczone.dappermatic/mjczone.dappermatic.converters/",
                },
              ],
            },
          ],
        },
      ],
    },

    socialLinks: [
      {
        icon: "github",
        link: "https://github.com/mjczone/dappermatic",
      },
    ],

    search: {
      provider: "local",
    },
  },
});
