import { useState } from 'react';
import type { Photo } from '../domain/catalog';

export function ReferencePhoto({ photo }: { photo: Photo }) {
  const [failed, setFailed] = useState(false);
  return <figure className="reference-photo">
    {failed ? <p role="alert">Reference photo could not load. <a href={photo.sourceUrl} target="_blank" rel="noreferrer">View the original</a>.</p>
      : <a href={photo.src} target="_blank" rel="noreferrer" title="Open full reference photo">
        <img src={photo.src} alt={photo.title} loading="lazy" onError={() => setFailed(true)} />
      </a>}
    <figcaption>
      <strong>{photo.title}</strong>
      <span>{photo.note}</span>
      <span><a href={photo.sourceUrl} target="_blank" rel="noreferrer">{photo.author}</a> · <a href={photo.licenseUrl} target="_blank" rel="noreferrer">{photo.license}</a></span>
    </figcaption>
  </figure>;
}
