import { spawnSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const docsV4Dir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const repoDir = path.resolve(docsV4Dir, '..')
const v3SrcDir = path.join(repoDir, 'docs')
const v3DefaultOutDir = path.join(v3SrcDir, '.vitepress', 'dist')
const v3OutDir = path.join(docsV4Dir, 'public', 'v3')

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    stdio: 'inherit',
    shell: process.platform === 'win32',
    ...options,
  })
  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(' ')} failed with code ${result.status}`)
  }
}

function ensureV3Dependencies() {
  console.log('[build-v3] Installing VitePress dependencies (docs/)...')
  run('npm', ['ci'], { cwd: v3SrcDir })
}

// VitePress emits directory index pages as `dir/index.html`, but links to
// them as `/dir`. Copy each `dir/index.html` to a sibling `dir.html` so the
// Next.js clean-URL fallback rewrite (`/v3/:path*` -> `/v3/:path*.html`)
// resolves directory index pages too.
function emitDirectoryIndexCopies(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true })
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      emitDirectoryIndexCopies(fullPath)
      const indexFile = path.join(fullPath, 'index.html')
      if (fs.existsSync(indexFile)) {
        fs.copyFileSync(indexFile, path.join(dir, `${entry.name}.html`))
      }
    }
  }
}

ensureV3Dependencies()

console.log('[build-v3] Cleaning previous V3 output...')
fs.rmSync(v3OutDir, { recursive: true, force: true })

console.log('[build-v3] Building VitePress V3 site...')
run('npx', ['vitepress', 'build', '.'], { cwd: v3SrcDir })

console.log('[build-v3] Emitting directory index copies for clean URLs...')
emitDirectoryIndexCopies(v3DefaultOutDir)

console.log(`[build-v3] Copying V3 output to ${v3OutDir}...`)
fs.cpSync(v3DefaultOutDir, v3OutDir, { recursive: true })

console.log(`[build-v3] V3 site built into ${v3OutDir}`)
