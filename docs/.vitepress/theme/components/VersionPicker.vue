<script setup>
import { ref, onMounted, watch } from 'vue'
import { useRouter, useData } from 'vitepress'

const { page } = useData()
const router = useRouter()
const currentVersion = ref('v2')

const versions = [
  { text: 'v2.x (Latest)', value: 'v2' },
  { text: 'v1.x (Legacy)', value: 'v1' }
]

onMounted(() => {
  const saved = localStorage.getItem('preferredVersion')
  const path = page.value.relativePath
  
  if (saved) {
    currentVersion.value = saved
    
    // Auto-redirect if on wrong version
    if (saved === 'v1' && path.startsWith('guide/')) {
      switchVersion('v1')
    } else if (saved === 'v2' && path.startsWith('v1/')) {
      switchVersion('v2')
    }
  }
  updateVersionFromRoute()
})

const updateVersionFromRoute = () => {
  if (page.value.relativePath.startsWith('v1/')) {
    currentVersion.value = 'v1'
  } else if (page.value.relativePath.startsWith('guide/')) {
    currentVersion.value = 'v2'
  }
}

watch(() => page.value.relativePath, updateVersionFromRoute)

const switchVersion = (version) => {
  currentVersion.value = version
  localStorage.setItem('preferredVersion', version)

  const path = page.value.relativePath
  let newPath = ''

  if (version === 'v1') {
    // Switch to v1
    if (path.startsWith('guide/')) {
      newPath = path.replace('guide/', 'v1/')
    } else {
      newPath = 'v1/getting-started'
    }
  } else {
    // Switch to v2
    if (path.startsWith('v1/')) {
      newPath = path.replace('v1/', 'guide/')
    } else {
      newPath = 'guide/getting-started'
    }
  }

  // Ensure path ends in .html or strip .md for VitePress routing
  newPath = '/' + newPath.replace(/\.md$/, '')
  
  router.go(newPath).catch(() => {
    // Fallback to version root if page doesn't exist in target version
    router.go(version === 'v1' ? '/v1/getting-started' : '/guide/getting-started')
  })
}
</script>

<template>
  <div class="version-picker">
    <select v-model="currentVersion" @change="switchVersion(currentVersion)">
      <option v-for="v in versions" :key="v.value" :value="v.value">
        {{ v.text }}
      </option>
    </select>
  </div>
</template>

<style scoped>
.version-picker {
  display: flex;
  align-items: center;
  margin-left: 1rem;
}

select {
  background: var(--vp-c-bg-soft);
  border: 1px solid var(--vp-c-divider);
  border-radius: 4px;
  padding: 4px 8px;
  color: var(--vp-c-text-1);
  font-size: 0.9rem;
  cursor: pointer;
  outline: none;
}

select:hover {
  border-color: var(--vp-c-brand);
}
</style>
