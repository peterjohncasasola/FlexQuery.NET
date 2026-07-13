import DefaultTheme from 'vitepress/theme'
import Layout from './Layout.vue'
import './custom.css'
import type { EnhanceAppContext } from 'vitepress'

function setPageMode(path: string): void {
  const vpDoc = document.querySelector('.VPDoc')
  if (!vpDoc) return

  vpDoc.classList.remove('page-guide', 'page-performance', 'page-examples', 'page-comparison', 'page-reference', 'page-home')

  if (path === '/' || path === '/index.html') {
    vpDoc.classList.add('page-home')
    return
  }

  if (path.startsWith('/guide/performance/')) {
    vpDoc.classList.add('page-performance')
    return
  }

  if (
    path === '/guide/comparison' ||
    path === '/guide/comparison-libraries' ||
    path === '/comparison/graphql-odata' ||
    path === '/guide/dotnet-comparison'
  ) {
    vpDoc.classList.add('page-comparison')
    return
  }

  if (path.startsWith('/examples/')) {
    vpDoc.classList.add('page-examples')
    return
  }

  if (path.startsWith('/guide/') || path.startsWith('/migration/')) {
    vpDoc.classList.add('page-guide')
    return
  }

  vpDoc.classList.add('page-reference')
}

export default {
  ...DefaultTheme,
  enhanceApp({ router }: EnhanceAppContext) {
    if (typeof window === 'undefined') return

    const setPageModeWhenReady = (path: string) => {
      const vpDoc = document.querySelector('.VPDoc')
      if (vpDoc) {
        setPageMode(path)
      } else {
        requestAnimationFrame(() => setPageModeWhenReady(path))
      }
    }

    setPageModeWhenReady(decodeURIComponent(window.location.pathname))

    router.onAfterRouteChanged = (to: string) => {
      const el = document.querySelector('.VPSidebar') as HTMLElement | null
      if (el) {
        el.style.display = ''
        el.removeAttribute('inert')
      }

      const mask = document.querySelector('.VPSidebarMask') as HTMLElement | null
      if (mask) {
        mask.style.display = 'none'
      }

      setPageModeWhenReady(to)
    }
  }
}
