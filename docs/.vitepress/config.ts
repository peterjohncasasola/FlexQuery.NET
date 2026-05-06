import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'FlexQuery.NET',
  description:
    'A dynamic query engine for .NET APIs — filtering, sorting, paging, projection, validation, and field-level security over IQueryable.',

  cleanUrls: true,

  head: [
    ['link', { rel: 'icon', href: '/logo.png' }],
    ['meta', { name: 'theme-color', content: '#646cff' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'FlexQuery.NET' }],
    [
      'meta',
      {
        property: 'og:description',
        content:
          'Dynamic filtering, sorting, projection & nested querying for .NET IQueryable'
      }
    ],
    ['link', { rel: 'preconnect', href: 'https://fonts.googleapis.com' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' }],
    ['link', { href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap', rel: 'stylesheet' }]
  ],

  themeConfig: {
    logo: '/logo.png',

    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'Examples', link: '/examples/basic' },
      { text: 'Migration v1→v2', link: '/migration' },
      {
        text: 'Version',
        items: [
          { text: 'v2.x (Latest)', link: '/guide/getting-started' },
          { text: 'v1.x (Legacy)', link: '/v1/getting-started' }
        ]
      },
      {
        text: 'Links',
        items: [
          {
            text: 'Changelog',
            link: 'https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/CHANGELOG.md'
          },
          {
            text: 'NuGet',
            link: 'https://www.nuget.org/packages/FlexQuery.NET'
          }
        ]
      }
    ],

    sidebar: {
      '/guide/': [
        {
          text: 'Introduction',
          items: [
            { text: 'Why FlexQuery.NET?',       link: '/guide/why-flexquery' },
            { text: 'Getting Started',           link: '/guide/getting-started' },
            { text: 'How It Works',              link: '/guide/how-it-works' },
            { text: 'Core Concepts',             link: '/guide/core-concepts' }
          ]
        },
        {
          text: 'Query Features',
          items: [
            { text: 'Filtering',                 link: '/guide/filtering' },
            { text: 'Sorting',                   link: '/guide/sorting' },
            { text: 'Paging',                    link: '/guide/paging' },
            { text: 'Projection',                link: '/guide/projection' },
            { text: 'Flattening Modes',          link: '/guide/flattening' },
            { text: 'Grouping & Aggregates',     link: '/guide/grouping' },
            { text: 'Include Filtering',         link: '/guide/include-filtering' },
            { text: 'Query Composition',         link: '/guide/query-composition' },
          ]
        },
        {
          text: 'Execution & Security',
          items: [
            { text: 'Execution Pipeline',        link: '/guide/execution' },
            { text: 'Validation',                link: '/guide/validation' },
            { text: 'Security & Field Access',   link: '/guide/security' }
          ]
        },
        {
          text: 'Integration',
          items: [
            { text: 'ASP.NET Core',              link: '/guide/aspnet-integration' },
            { text: 'Swagger Integration',       link: '/guide/swagger-integration' },
            { text: 'Performance',               link: '/guide/performance' },
            { text: 'Debugging',                 link: '/guide/debugging' },
          ]
        },
        {
          text: 'Comparisons',
          items: [
            { text: 'vs GraphQL & OData',        link: '/guide/comparison' },
            { text: 'vs .NET Libraries',         link: '/guide/dotnet-comparison' }
          ]
        },
        {
          text: 'Reference',
          items: [
            { text: 'Query Language',            link: '/shared/query-language' },
            { text: 'Operators',                 link: '/shared/operators' },
            { text: 'Migration v1 → v2',         link: '/migration' }
          ]
        }
      ],

      '/examples/': [
        {
          text: 'Examples',
          items: [
            { text: 'Basic Examples',            link: '/examples/basic' },
            { text: 'Advanced Examples',         link: '/examples/advanced' },
            { text: 'Real-World Scenarios',      link: '/examples/real-world' }
          ]
        }
      ],

      '/v1/': [
        {
          text: 'FlexQuery v1 (Legacy)',
          collapsed: true,
          items: [
            { text: 'Introduction',              link: '/v1/introduction' },
            { text: 'Getting Started',           link: '/v1/getting-started' },
            { text: 'How it Works',              link: '/v1/how-it-works' },
            { text: 'Filtering',                 link: '/v1/filtering' },
            { text: 'Sorting',                   link: '/v1/sorting' },
            { text: 'Projection',                link: '/v1/projection' },
            { text: 'Includes',                  link: '/v1/include' },
            { text: 'Grouping',                  link: '/v1/grouping' },
            { text: 'Security',                  link: '/v1/security' },
            { text: 'Strongly-Typed Queries',    link: '/v1/strongly-typed-query' },
            { text: 'ASP.NET Integration',       link: '/v1/aspnet-integration' },
            { text: 'Swagger Integration',       link: '/v1/swagger-integration' },
            { text: 'Performance',               link: '/v1/performance' },
            { text: 'Debugging',                 link: '/v1/debugging' }
          ]
        },
        {
          text: 'Reference',
          items: [
            { text: 'Query Language',            link: '/shared/query-language' },
            { text: 'Operators',                 link: '/shared/operators' }
          ]
        }
      ]
    },

    socialLinks: [
      {
        icon: 'github',
        link: 'https://github.com/peterjohncasasola/FlexQuery.NET'
      }
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 FlexQuery.NET Contributors'
    },

    editLink: {
      pattern:
        'https://github.com/peterjohncasasola/FlexQuery.NET/edit/main/docs/:path',
      text: 'Edit this page on GitHub'
    },

    search: {
      provider: 'local'
    }
  },

  base: '/'
})
