import type { ReactNode } from 'react'

const tokenPattern = /("(?:\\.|[^"\\])*")(\s*:)?|\b(true|false)\b|\bnull\b|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/g
const urlPattern = /^https?:\/\/\S+$/i

function parseString(token: string): string {
  try {
    return JSON.parse(token) as string
  } catch {
    return token
  }
}

/** Colorizes pretty-printed JSON; string values that are http(s) URLs become links opening in a new tab. */
export function JsonView({ text }: { text: string }) {
  const nodes: ReactNode[] = []
  let cursor = 0
  let key = 0

  for (const match of text.matchAll(tokenPattern)) {
    const index = match.index ?? 0
    if (index > cursor) nodes.push(text.slice(cursor, index))

    const [token, quoted, colon, bool] = match
    if (quoted && colon) {
      nodes.push(
        <span key={key++} className="json-key">
          {quoted}
        </span>,
        colon
      )
    } else if (quoted) {
      const value = parseString(quoted)
      nodes.push(
        urlPattern.test(value) ? (
          <span key={key++} className="json-string">
            &quot;
            <a className="json-link" href={value} target="_blank" rel="noopener noreferrer">
              {quoted.slice(1, -1)}
            </a>
            &quot;
          </span>
        ) : (
          <span key={key++} className="json-string">
            {quoted}
          </span>
        )
      )
    } else {
      const kind = bool ? 'json-boolean' : token === 'null' ? 'json-null' : 'json-number'
      nodes.push(
        <span key={key++} className={kind}>
          {token}
        </span>
      )
    }
    cursor = index + token.length
  }

  if (cursor < text.length) nodes.push(text.slice(cursor))
  return <>{nodes}</>
}
