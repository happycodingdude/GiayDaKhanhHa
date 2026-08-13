import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AppProviders } from './app/providers/AppProviders'
import './index.css'

const container = document.getElementById('root')
if (!container) throw new Error('Root element #root was not found.')

createRoot(container).render(
  <StrictMode>
    <AppProviders />
  </StrictMode>,
)
