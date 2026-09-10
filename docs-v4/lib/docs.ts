import fs from 'node:fs'
import path from 'node:path'
import matter from 'gray-matter'
import GithubSlugger from 'github-slugger'

const CONTENT_DIR = path.join(process.cwd(), 'content', 'docs')

export interface DocFrontmatter {
  title: string
  description: string
  section?: string
}

export interface Heading {
  text: string
  id: string
  level: number
}

export interface DocSource {
  modulePath: string
  frontmatter: DocFrontmatter
  headings: Heading[]
}

function walkMdxFiles(dir: string, base: string = ''): string[] {
  const results: string[] = []
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const rel = base ? `${base}/${entry.name}` : entry.name
    if (entry.isDirectory()) {
      results.push(...walkMdxFiles(path.join(dir, entry.name), rel))
    } else if (entry.isFile() && entry.name.endsWith('.mdx')) {
      results.push(rel)
    }
  }
  return results
}

export function getAllDocSlugs(): string[][] {
  if (!fs.existsSync(CONTENT_DIR)) return []
  return walkMdxFiles(CONTENT_DIR).map((file) => {
    const withoutExt = file.replace(/\.mdx$/, '')
    if (withoutExt.endsWith('/index')) {
      return withoutExt.slice(0, -'/index'.length).split('/')
    }
    return withoutExt.split('/')
  })
}

export function docModulePath(slug: string[]): string | null {
  const joined = slug.join('/')
  const candidates = [`${joined}.mdx`, `${joined}/index.mdx`]
  for (const candidate of candidates) {
    const full = path.join(CONTENT_DIR, ...candidate.split('/'))
    if (fs.existsSync(full)) return candidate
  }
  return null
}

export function readDocSource(slug: string[]): DocSource | null {
  const modulePath = docModulePath(slug)
  if (!modulePath) return null
  const filePath = path.join(CONTENT_DIR, ...modulePath.split('/'))
  const raw = fs.readFileSync(filePath, 'utf8')
  const { data } = matter(raw)
  const slugger = new GithubSlugger()

  const headings: Heading[] = []
  let inCodeFence = false
  for (const line of raw.split('\n')) {
    const trimmed = line.trimStart()
    if (trimmed.startsWith('```') || trimmed.startsWith('~~~')) {
      inCodeFence = !inCodeFence
      continue
    }
    if (inCodeFence) continue
    const match = /^(#{2,3})\s+(.+)$/.exec(trimmed)
    if (match) {
      const text = match[2].replace(/[#*`]/g, '').trim()
      headings.push({ text, id: slugger.slug(text), level: match[1].length })
    }
  }

  return {
    modulePath,
    frontmatter: {
      title: (data.title as string) ?? slug[slug.length - 1],
      description: (data.description as string) ?? '',
      section: data.section as string | undefined,
    },
    headings,
  }
}
