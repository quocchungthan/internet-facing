import type { ReactNode } from 'react'

const ICONS: Record<string, ReactNode> = {
  github: <path fill="currentColor" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0 1 12 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0 0 22 12.017C22 6.484 17.522 2 12 2Z" />,
  tiktok: <path fill="currentColor" d="M14 2h3c0 3 1.6 4.8 5 5v3a9 9 0 0 1-5-1.5V16a6 6 0 1 1-6-6v3a3 3 0 1 0 3 3Z" />,
  facebook: <path fill="currentColor" d="M14 22v-9h3l.5-4H14V7c0-1.1.3-2 2-2h2V1.4A26 26 0 0 0 15 1c-3 0-5 1.8-5 5v3H7v4h3v9Z" />,
  instagram: <><rect x="3" y="3" width="18" height="18" rx="5" /><circle cx="12" cy="12" r="4" /><circle cx="17.5" cy="6.5" r="1" fill="currentColor" stroke="none" /></>,
  'azure devops': <path fill="currentColor" stroke="none" d="m2 7 5-4v4l8-3V1l7 5v13l-7 4v-4l-8-3v4l-5-4 5-2V9Zm5 2v5l8 3V6Z" />,
  gmail: <><rect x="2" y="4" width="20" height="16" rx="2" /><path d="m3 5 9 7 9-7M3 19V8l9 7 9-7v11" /></>,
}

export function PlatformIcon({ platform }: { platform: string }) {
  return (
    <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth={['github', 'tiktok', 'facebook'].includes(platform.trim().toLowerCase()) ? 0 : 1.75} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
      {ICONS[platform.trim().toLowerCase()] ?? <><circle cx="12" cy="12" r="9" /><ellipse cx="12" cy="12" rx="4" ry="9" /><path d="M3 12h18" /></>}
    </svg>
  )
}
