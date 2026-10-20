import { lazy, Suspense } from 'react';
import { features } from './config/features';
import './styles.css';

const ExplorationWorkshop = lazy(() => import('./features/exploration/presentation/ExplorationWorkshop'));
const ContentWorkshop = lazy(() => import('./components/ContentWorkshop'));

export default function App() {
  return <Suspense fallback={<p role="status">Opening your workshop...</p>}>
    {features.exploration ? <ExplorationWorkshop /> : <ContentWorkshop />}
  </Suspense>;
}
