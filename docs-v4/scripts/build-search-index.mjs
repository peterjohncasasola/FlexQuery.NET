import fs from 'node:fs'
import path from 'node:path'
import matter from 'gray-matter'

const CONTENT_DIR = path.join(process.cwd(), 'content', 'docs')
const OUT_FILE = path.join(process.cwd(), 'public', 'search-index.json')

function walkMdxFiles(dir, base = '') {
  const results = []
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

function slugToRoute(file) {
  const withoutExt = file.replace(/\.mdx$/, '')
  const clean = withoutExt.endsWith('/index') ? withoutExt.slice(0, -'/index'.length) : withoutExt
  return clean
}

function stripMdx(raw) {
  return raw
    .replace(/^---\n[\s\S]*?\n---/, '')
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/~~~[\s\S]*?~~~/g, ' ')
    .replace(/<[^>]+>/g, ' ')
    .replace(/import\s+.*from\s+.*/g, ' ')
    .replace(/[#*`>[\]()!]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

function main() {
  if (!fs.existsSync(CONTENT_DIR)) {
    console.error('content/docs not found')
    process.exit(1)
  }
  const files = walkMdxFiles(CONTENT_DIR)
  const index = files.map((file) => {
    const raw = fs.readFileSync(path.join(CONTENT_DIR, file), 'utf8')
    const { data, content } = matter(raw)
    const headings = []
    let inFence = false
    for (const line of content.split('\n')) {
      const t = line.trimStart()
      if (t.startsWith('```') || t.startsWith('~~~')) {
        inFence = !inFence
        continue
      }
      if (inFence) continue
      const m = /^(#{1,4})\s+(.+)$/.exec(t)
      if (m) headings.push({ text: m[2].replace(/[#*`]/g, '').trim(), id: m[2].toLowerCase().replace(/[^\w\s-]/g, '').trim().replace(/\s+/g, '-') })
    }
    return {
      title: data.title ?? file,
      description: data.description ?? '',
      section: data.section ?? '',
      slug: slugToRoute(file),
      headings,
      body: stripMdx(content).slice(0, 6000),
    }
  })
  fs.mkdirSync(path.dirname(OUT_FILE), { recursive: true })
  fs.writeFileSync(OUT_FILE, JSON.stringify(index))
  console.log(`search-index.json: ${index.length} pages`)
}

main()
