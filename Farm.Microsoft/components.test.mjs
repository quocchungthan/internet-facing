import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { registerHooks } from 'node:module'
import test from 'node:test'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import ts from 'typescript'

registerHooks({
  load(url, context, nextLoad) {
    if (!url.endsWith('.tsx')) return nextLoad(url, context)
    const { outputText } = ts.transpileModule(readFileSync(new URL(url), 'utf8'), {
      compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.ESNext },
    })
    return { format: 'module', source: outputText, shortCircuit: true }
  },
})

const { LinkedText } = await import('./src/LinkedText.tsx')
const { PlatformIcon } = await import('./src/PlatformIcon.tsx')
const renderText = (text) => renderToStaticMarkup(createElement(LinkedText, { text }))

test('links plain URLs without swallowing surrounding punctuation or newlines', () => {
  assert.equal(
    renderText('See https://example.com/demo, then (https://example.com/wiki/Thing_(software)).\nDone.'),
    'See <a href="https://example.com/demo" target="_blank" rel="noopener noreferrer">https://example.com/demo</a>, then (<a href="https://example.com/wiki/Thing_(software)" target="_blank" rel="noopener noreferrer">https://example.com/wiki/Thing_(software)</a>).\nDone.',
  )
})

test('renders adjacent Markdown links and balanced URL parentheses', () => {
  assert.equal(
    renderText('[Demo](https://example.com/demo)[Docs](https://example.com/wiki/Thing_(software))'),
    '<a href="https://example.com/demo" target="_blank" rel="noopener noreferrer">Demo</a><a href="https://example.com/wiki/Thing_(software)" target="_blank" rel="noopener noreferrer">Docs</a>',
  )
})

test('keeps HTML and unsafe Markdown links as escaped text', () => {
  assert.equal(
    renderText('<script>alert(1)</script> [bad](javascript:alert(1))'),
    '&lt;script&gt;alert(1)&lt;/script&gt; [bad](javascript:alert(1))',
  )
})

test('renders all configured platform marks and an SVG fallback', () => {
  const marks = ['GitHub', 'TikTok', 'Facebook', 'Instagram', 'Azure DevOps', 'Gmail', 'Unknown']
    .map((platform) => renderToStaticMarkup(createElement(PlatformIcon, { platform })))
  for (const mark of marks) {
    assert.match(mark, /^<svg /)
    assert.match(mark, /aria-hidden="true"/)
    assert.doesNotMatch(mark, /<text/)
  }
  assert.equal(new Set(marks).size, marks.length)
})
