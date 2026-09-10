import fs from 'node:fs'
import path from 'node:path'

const CONTENT_DIR = path.join(process.cwd(), 'content', 'docs')

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
  return withoutExt.endsWith('/index') ? withoutExt.slice(0, -'/index'.length) : withoutExt
}

function main() {
  const slugs = new Set(walkMdxFiles(CONTENT_DIR).map(slugToRoute))
  const errors = []
  const mdxFiles = walkMdxFiles(CONTENT_DIR)

  for (const file of mdxFiles) {
    const raw = fs.readFileSync(path.join(CONTENT_DIR, file), 'utf8')
    const linkRe = /\[[^\]]*\]\((\/[^)#\s]*)(#[^)\s]*)?\)/g
    let m
    while ((m = linkRe.exec(raw)) !== null) {
      const href = m[1]
      if (href.startsWith('http')) continue
      let target = href.replace(/^\/docs\/?/, '')
      if (target === '') {
        if (!slugs.has('introduction')) errors.push(`${file}: broken link ${href}`)
        continue
      }
      const exists = slugs.has(target) || [...slugs].some((s) => target.startsWith(s + '/'))
      if (!exists) errors.push(`${file}: broken link ${href}`)
    }
  }

  if (errors.length > 0) {
    console.error('Broken links found:')
    for (const e of errors) console.error(' - ' + e)
    process.exit(1)
  }
  console.log(`Link check passed (${mdxFiles.length} files).`)
}

main()
