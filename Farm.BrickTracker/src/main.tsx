import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrickTracker } from './BrickTracker'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '2rem 1rem' }}>
      <BrickTracker />
    </div>
  </StrictMode>
)
