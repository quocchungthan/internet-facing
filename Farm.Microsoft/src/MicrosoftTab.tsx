import { useEffect, useState, useCallback } from 'react'
import './themes/microsoft/microsoft.scss'

interface ApiTracingMetrics {
  totalRequests: number | null
  uniqueClients: number | null
  rateLimitLimit: number | null
  rateLimitRemaining: number | null
  rateLimitReset: number | null
  clientStatus: string
}

interface ApiBlockState {
  data: unknown | null
  rawJson: string
  status: number | null
  statusText: string
  loading: boolean
  error: string | null
  lastUpdated: string | null
  metrics: ApiTracingMetrics
}

const initialMetrics: ApiTracingMetrics = {
  totalRequests: null,
  uniqueClients: null,
  rateLimitLimit: null,
  rateLimitRemaining: null,
  rateLimitReset: null,
  clientStatus: 'Idle',
}

const initialBlockState: ApiBlockState = {
  data: null,
  rawJson: '',
  status: null,
  statusText: '',
  loading: false,
  error: null,
  lastUpdated: null,
  metrics: initialMetrics,
}

// Generate or retrieve persistent unique client ID
function getOrCreateClientId(): string {
  try {
    const key = 'farm_client_id'
    let id = localStorage.getItem(key)
    if (!id) {
      id = 'client_' + Math.random().toString(36).substring(2, 10) + Date.now().toString(36)
      localStorage.setItem(key, id)
    }
    return id
  } catch {
    return 'browser-client-' + Math.random().toString(36).substring(2, 10)
  }
}

export const MicrosoftTab = () => {
  const [metadataBlock, setMetadataBlock] = useState<ApiBlockState>(initialBlockState)
  const [feedsBlock, setFeedsBlock] = useState<ApiBlockState>(initialBlockState)
  const clientId = getOrCreateClientId()

  const fetchEndpoint = useCallback(
    async (
      endpoint: string,
      setBlock: React.Dispatch<React.SetStateAction<ApiBlockState>>
    ) => {
      setBlock((prev) => ({
        ...prev,
        loading: true,
        error: null,
        statusText: 'Fetching...',
      }))

      try {
        const response = await fetch(endpoint, {
          method: 'GET',
          headers: {
            Accept: 'application/json',
            'X-Client-Id': clientId,
          },
        })

        // Extract tracing headers populated by backend client architecture
        const totalReqsHeader = response.headers.get('X-Total-Requests')
        const uniqueClientsHeader = response.headers.get('X-Unique-Clients')
        const limitHeader = response.headers.get('X-RateLimit-Limit')
        const remainingHeader = response.headers.get('X-RateLimit-Remaining')
        const resetHeader = response.headers.get('X-RateLimit-Reset')
        const clientStatusHeader = response.headers.get('X-Client-Status')

        let parsedData: unknown = null
        let jsonString = ''

        try {
          parsedData = await response.json()
          jsonString = JSON.stringify(parsedData, null, 2)
        } catch {
          jsonString = await response.text()
        }

        const metrics: ApiTracingMetrics = {
          totalRequests: totalReqsHeader ? parseInt(totalReqsHeader, 10) : null,
          uniqueClients: uniqueClientsHeader ? parseInt(uniqueClientsHeader, 10) : null,
          rateLimitLimit: limitHeader ? parseInt(limitHeader, 10) : null,
          rateLimitRemaining: remainingHeader ? parseInt(remainingHeader, 10) : null,
          rateLimitReset: resetHeader ? parseInt(resetHeader, 10) : null,
          clientStatus: clientStatusHeader || (response.ok ? 'OK' : 'Error'),
        }

        setBlock({
          data: parsedData,
          rawJson: jsonString,
          status: response.status,
          statusText: `${response.status} ${response.statusText || (response.ok ? 'OK' : 'Error')}`,
          loading: false,
          error: response.ok ? null : `HTTP Error ${response.status}`,
          lastUpdated: new Date().toLocaleTimeString(),
          metrics,
        })
      } catch (err: unknown) {
        const errMsg = err instanceof Error ? err.message : String(err)
        setBlock((prev) => ({
          ...prev,
          loading: false,
          status: 0,
          statusText: 'Network Error',
          error: errMsg,
          lastUpdated: new Date().toLocaleTimeString(),
        }))
      }
    },
    [clientId]
  )

  // Load both endpoints on open (mount)
  useEffect(() => {
    fetchEndpoint('/api/metadata', setMetadataBlock)
    fetchEndpoint('/api/feeds', setFeedsBlock)
  }, [fetchEndpoint])

  const renderApiBlock = (
    endpoint: string,
    title: string,
    block: ApiBlockState,
    onFetch: () => void
  ) => {
    const isSuccess = block.status && block.status >= 200 && block.status < 300
    const isRateLimited = block.status === 429
    const statusClass = block.loading
      ? 'status-loading'
      : isSuccess
      ? 'status-success'
      : 'status-error'

    return (
      <section className="ms-get-block" aria-label={`API block for ${endpoint}`}>
        <div className="ms-api-bar">
          <div className="ms-api-left">
            <span className="ms-method-badge">GET</span>
            <span className="ms-endpoint-path">{endpoint}</span>

            {block.status !== null && (
              <span className={`ms-status-badge ${statusClass}`}>
                {block.statusText}
              </span>
            )}

            <div className="ms-metrics-group">
              {block.metrics.totalRequests !== null && (
                <div className="ms-metric-pill" title="Total requests served by Farm backend">
                  <span>Reqs:</span>
                  <span className="metric-value">{block.metrics.totalRequests}</span>
                </div>
              )}

              {block.metrics.uniqueClients !== null && (
                <div className="ms-metric-pill" title="Unique clients tracked by Farm backend">
                  <span>Clients:</span>
                  <span className="metric-value">{block.metrics.uniqueClients}</span>
                </div>
              )}

              {block.metrics.rateLimitRemaining !== null && (
                <div
                  className={`ms-metric-pill rate-limit-pill ${
                    isRateLimited || block.metrics.rateLimitRemaining < 5
                      ? 'limit-warn'
                      : 'limit-ok'
                  }`}
                  title="Requests remaining in current rate limit window"
                >
                  <span>Rate limit:</span>
                  <span className="metric-value">
                    {block.metrics.rateLimitRemaining}/{block.metrics.rateLimitLimit ?? 60} left
                  </span>
                  {block.metrics.rateLimitReset !== null && (
                    <span style={{ opacity: 0.7 }}>
                      ({block.metrics.rateLimitReset}s reset)
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>

          <div className="ms-api-right">
            <button
              type="button"
              className="ms-btn-get"
              onClick={onFetch}
              disabled={block.loading}
              aria-label={`Send GET request to ${endpoint}`}
            >
              {block.loading ? 'Fetching...' : 'GET'}
            </button>
          </div>
        </div>

        <div className="ms-code-block-wrapper">
          <pre tabIndex={0}>
            {block.loading && !block.rawJson
              ? '// Loading response data...'
              : block.rawJson || '// Click GET to invoke request'}
          </pre>
        </div>

        <div className="ms-block-footer">
          <span>{title}</span>
          {block.lastUpdated && <span>Updated: {block.lastUpdated}</span>}
        </div>
      </section>
    )
  }

  return (
    <div className="ms-view-container">
      <header className="ms-header">
        <div className="ms-title-group">
          <div className="ms-icon-tile">⊞</div>
          <div>
            <h2>Elder Vibe Coder</h2>
          </div>
        </div>
        <p className="ms-subtitle">
          If you are seeking for a software developer to talk to, it's me here.
        </p>
      </header>

      {/* Block 1: GET /api/metadata */}
      {renderApiBlock(
        '/api/metadata',
        'Metadata',
        metadataBlock,
        () => fetchEndpoint('/api/metadata', setMetadataBlock)
      )}

      {/* Block 2: GET /api/feeds */}
      {renderApiBlock(
        '/api/feeds',
        'Feeds',
        feedsBlock,
        () => fetchEndpoint('/api/feeds', setFeedsBlock)
      )}
    </div>
  )
}

export default MicrosoftTab
