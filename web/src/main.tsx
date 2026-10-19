import { StrictMode, Component, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';

class AppBoundary extends Component<{ children: ReactNode }, { message: string }> {
  state = { message: '' };
  static getDerivedStateFromError(error: Error) { return { message: error.message }; }
  render() {
    return this.state.message ? <main className="fatal-error"><h1>The workshop could not open.</h1><p role="alert">{this.state.message}</p><p>Reload the page. If this persists, the vehicle content or application needs attention.</p></main> : this.props.children;
  }
}

createRoot(document.getElementById('root')!).render(<StrictMode><AppBoundary><App /></AppBoundary></StrictMode>);
