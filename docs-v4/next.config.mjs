import createMDX from '@next/mdx'

/** @type {import('next').NextConfig} */
const nextConfig = {
  pageExtensions: ['js', 'jsx', 'ts', 'tsx', 'md', 'mdx'],
  async rewrites() {
    return {
      // Serve the legacy VitePress V3 static site (built into public/v3)
      // under /v3. VitePress cleanUrls emits .html files and links without
      // the extension, so fall back to appending .html only when no static
      // file matched (fallback rewrites run after the filesystem check,
      // keeping /v3 assets and existing .html files untouched).
      beforeFiles: [
        { source: '/v3', destination: '/v3/index.html' },
        { source: '/v3/', destination: '/v3/index.html' },
      ],
      fallback: [{ source: '/v3/:path*', destination: '/v3/:path*.html' }],
    }
  },
}

const withMDX = createMDX({
  options: {
    remarkPlugins: [
      'remark-gfm',
      'remark-frontmatter',
      ['remark-mdx-frontmatter', { name: 'frontmatter' }],
    ],
    rehypePlugins: [
      'rehype-slug',
      [
        '@shikijs/rehype',
        {
          themes: {
            light: 'github-light',
            dark: 'github-dark',
          },
          defaultLanguage: 'text',
          addLanguageClass: true,
        },
      ],
    ],
  },
})

export default withMDX(nextConfig)
