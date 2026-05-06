<script setup>
import DefaultTheme from 'vitepress/theme'
import VersionPicker from './components/VersionPicker.vue'
import VersionBanner from './components/VersionBanner.vue'

const { Layout } = DefaultTheme
</script>

<template>
  <Layout>
    <template #nav-bar-content-after>
      <VersionPicker />
    </template>
    
    <template #doc-before>
      <VersionBanner />
    </template>
  </Layout>
</template>
