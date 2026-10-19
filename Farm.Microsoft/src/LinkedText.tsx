import type { ReactNode } from 'react'

export function LinkedText({ text }: { text: string }) {
  const parts: ReactNode[] = []
  const links = /\[([^\]\n]+)\]\((https?:\/\/(?:[^\s<>()]|\([^()\s<>]*\))+)\)|(https?:\/\/[^\s<>]+)/gi
  let cursor = 0

  for (const match of text.matchAll(links)) {
    const start = match.index
    let url = match[2] ?? match[3]
    let suffix = ''
    if (!match[2]) {
      while (/[.,!?;:]$/.test(url) || (url.endsWith(')') && url.split(')').length > url.split('(').length)) {
        suffix = url.slice(-1) + suffix
        url = url.slice(0, -1)
      }
    }
    parts.push(text.slice(cursor, start))
    parts.push(<a key={start} href={url} target="_blank" rel="noopener noreferrer">{match[1] ?? url}</a>, suffix)
    cursor = start + match[0].length
  }

  parts.push(text.slice(cursor))
  return <>{parts}</>
}
