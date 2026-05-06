<script setup>
import { computed } from 'vue'
import { useData } from 'vitepress'

const { page } = useData()

const isV1 = computed(() => page.value.relativePath.startsWith('v1/'))
const isV2 = computed(() => page.value.relativePath.startsWith('guide/'))

const v2Equivalent = computed(() => {
  if (isV1.value) {
    return '/' + page.value.relativePath.replace('v1/', 'guide/').replace(/\.md$/, '')
  }
  return null
})
</script>

<template>
  <div v-if="isV1" class="banner v1-banner">
    <span class="icon">⚠️</span>
    <div class="content">
      You are viewing <strong>legacy documentation (v1)</strong>. This version is deprecated.
      <div class="links">
        <a :href="v2Equivalent" class="link">Switch to v2</a>
        <span class="sep">|</span>
        <a href="/migration" class="link">Migration Guide</a>
      </div>
    </div>
  </div>

  <div v-else-if="isV2" class="banner v2-banner">
    <span class="icon">✅</span>
    <div class="content">
      You are viewing the <strong>latest version</strong> of FlexQuery.NET.
    </div>
  </div>
</template>

<style scoped>
.banner {
  padding: 12px 24px;
  margin-bottom: 24px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 0.95rem;
  line-height: 1.5;
}

.v1-banner {
  background-color: var(--vp-custom-block-warning-bg);
  color: var(--vp-custom-block-warning-text);
  border: 1px solid var(--vp-custom-block-warning-border);
}

.v2-banner {
  background-color: var(--vp-custom-block-tip-bg);
  color: var(--vp-custom-block-tip-text);
  border: 1px solid var(--vp-custom-block-tip-border);
}

.icon {
  font-size: 1.2rem;
}

.content {
  flex: 1;
}

.links {
  margin-top: 4px;
  display: flex;
  gap: 8px;
  align-items: center;
}

.link {
  color: inherit;
  text-decoration: underline;
  font-weight: 600;
}

.link:hover {
  opacity: 0.8;
}

.sep {
  opacity: 0.5;
}
</style>
