import DefaultTheme from 'vitepress/theme'
import Layout from './Layout.vue'
import './custom.css'
import type { EnhanceAppContext } from 'vitepress'

export default {
  ...DefaultTheme,
  // Layout,
  enhanceApp({ router }: EnhanceAppContext) {
    if (typeof window === 'undefined') return

    router.onAfterRouteChanged = () => {
      // Re-open the sidebar after navigation so it doesn't close on link click.
      // VitePress uses a data attribute on <html> to track the sidebar open state.
      const el = document.querySelector('.VPSidebar') as HTMLElement | null
      if (el) {
        // On narrow viewports VitePress removes aria-expanded; we just ensure
        // the sidebar keeps its visible class rather than being hidden.
        el.style.display = ''
        el.removeAttribute('inert')
      }

      // Keep the screen overlay clear — prevent the mask from re-appearing.
      const mask = document.querySelector('.VPSidebarMask') as HTMLElement | null
      if (mask) {
        mask.style.display = 'none'
      }
    }
  }
}
