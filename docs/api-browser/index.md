# API Browser

<script setup>
import { ApiReference } from '@scalar/api-reference'
import { useData } from 'vitepress'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import openApiSpec from './openapi-spec.js'

const { isDark } = useData()

// Create dynamic CSS that responds to VitePress theme
const dynamicCss = computed(() => `
  .scalar-api-reference {
    height: 100% !important;
    width: 100% !important;
    font-family: var(--vp-font-family-base);
    --scalar-background-1: ${isDark.value ? '#1e1e20' : '#ffffff'};
    --scalar-background-2: ${isDark.value ? '#2e2e32' : '#f6f6f7'};
    --scalar-background-3: ${isDark.value ? '#3e3e42' : '#f1f1f3'};
    --scalar-color-1: ${isDark.value ? '#f1f1f3' : '#2e2e32'};
    --scalar-color-2: ${isDark.value ? '#c9c9c9' : '#757575'};
    --scalar-color-3: ${isDark.value ? '#8e8e93' : '#8e8e93'};
    --scalar-border-color: ${isDark.value ? '#3e3e42' : '#e5e5e5'};
  }
  .scalar-api-reference .sidebar {
    border-right: 1px solid var(--vp-c-divider);
  }
  /* Reset VitePress heading margins within Scalar */
  .scalar-api-reference h1,
  .scalar-api-reference h2,
  .scalar-api-reference h3,
  .scalar-api-reference h4,
  .scalar-api-reference h5,
  .scalar-api-reference h6 {
    margin: 0 !important;
    padding: 0 !important;
  }
  /* Specifically target operation rows that might have h3 elements */
  .scalar-api-reference [class*="operation"] h3,
  .scalar-api-reference [class*="endpoint"] h3,
  .scalar-api-reference [class*="method"] h3 {
    margin: 0 !important;
    margin-block: 0 !important;
    margin-inline: 0 !important;
  }
  /* Hide Scalar's built-in theme switcher since we're using VitePress theme */
  .scalar-api-reference [class*="theme"] button,
  .scalar-api-reference [class*="Theme"] button,
  .scalar-api-reference [class*="darkmode"] button,
  .scalar-api-reference [class*="DarkMode"] button,
  .scalar-api-reference [data-testid*="theme"],
  .scalar-api-reference [aria-label*="theme" i],
  .scalar-api-reference [aria-label*="dark" i],
  .scalar-api-reference [aria-label*="light" i],
  .scalar-api-reference [title*="theme" i],
  .scalar-api-reference [title*="dark" i],
  .scalar-api-reference [title*="light" i] {
    display: none !important;
  }
  /* Reduce padding on description/introduction section */
  .scalar-api-reference #description,
  .scalar-api-reference #introduction {
    padding-top: 10px !important;
    padding-bottom: 10px !important;
  }
  /* Additional padding experiments can be done in .vitepress/theme/custom.css */
`)

// Use a ref for the configuration to make it reactive
const apiConfig = ref({
  content: openApiSpec,
  theme: 'light', // Set to light and override with CSS
  //layout: 'classic',
  showSidebar: true,
  customCss: dynamicCss.value
})

// Watch for theme changes and update the CSS
watch(dynamicCss, (newCss) => {
  apiConfig.value = {
    ...apiConfig.value,
    customCss: newCss
  }
}, { immediate: false })

// Store original styles to restore them when leaving the page
let originalBodyOverflow = ''
let originalDocPadding = ''
let originalContainerMaxWidth = ''
let originalContainerPadding = ''
let originalContentMaxWidth = ''
let originalContentPadding = ''

onMounted(() => {
  // Store original styles
  const doc = document.querySelector('.VPDoc')
  const container = document.querySelector('.container')
  const content = document.querySelector('.content')

  if (doc) {
    originalDocPadding = doc.style.padding
    doc.style.padding = '0'
  }

  if (container) {
    originalContainerMaxWidth = container.style.maxWidth
    originalContainerPadding = container.style.padding
    container.style.maxWidth = 'none'
    container.style.padding = '0'
  }

  if (content) {
    originalContentMaxWidth = content.style.maxWidth
    originalContentPadding = content.style.padding
    content.style.maxWidth = 'none'
    content.style.padding = '0'
  }

  // Store body overflow but DON'T set to hidden - let Scalar handle its own scrolling
  originalBodyOverflow = document.body.style.overflow
})

onUnmounted(() => {
  // Restore original styles
  const doc = document.querySelector('.VPDoc')
  const container = document.querySelector('.container')
  const content = document.querySelector('.content')

  if (doc) {
    doc.style.padding = originalDocPadding
  }

  if (container) {
    container.style.maxWidth = originalContainerMaxWidth
    container.style.padding = originalContainerPadding
  }

  if (content) {
    content.style.maxWidth = originalContentMaxWidth
    content.style.padding = originalContentPadding
  }

  // Restore body scroll
  document.body.style.overflow = originalBodyOverflow
})
</script>

<style scoped>
/* Uncomment to load test stylesheet */
/* @import url('./test-styles.css'); */
.api-reference-container {
  position: fixed;
  top: var(--vp-nav-height, 60px);
  left: 0;
  right: 0;
  bottom: 0;
  width: 100vw;
  height: calc(100vh - var(--vp-nav-height, 60px));
  z-index: 1;
  background: var(--vp-c-bg);
  overflow: auto;
}

/* Ensure the Scalar component fills the container */
.api-reference-container :deep(.scalar-api-reference) {
  height: auto !important;
  width: 100% !important;
  min-height: 100%;
}

/* Hide the page title since we're going full screen */
.api-reference-container ~ * {
  display: none;
}
</style>

<div class="api-reference-container">
  <ApiReference
    :configuration="apiConfig"
  />
</div>