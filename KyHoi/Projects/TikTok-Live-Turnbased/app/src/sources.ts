import type { CommentEvent } from './types.ts';
import { TikTokLiveConnection, ControlEvent, WebcastEvent } from 'tiktok-live-connector';
export interface CommentSource { start(): void; stop(): Promise<void> }
export class MockSource implements CommentSource { start() {} async stop() {} }

// Connector 2.5.0 uses v3 protobuf: content, user.id, common.msgId.
export function normalizeChat(data: { content?: string; user?: { id?: string }; common?: { msgId?: string; roomId?: string } }, now = Date.now()): CommentEvent | null {
  if (!data.user?.id || data.user.id === '0' || !data.common?.msgId || data.common.msgId === '0' || typeof data.content !== 'string') return null;
  if (data.content.length > 500) return null;
  return { eventId:`tiktok:${data.common.roomId ?? ''}:${data.common.msgId}`, userId:data.user.id, text:data.content, receivedAt:now };
}
export class TikTokSource implements CommentSource {
  private client: TikTokLiveConnection;
  private stopped = false;
  private retry?: NodeJS.Timeout;
  private delay = 2000;
  private resetting = false;
  constructor(username: string, key: string, private onComment: (event: CommentEvent) => void,
    private onConnection: (connected: boolean) => void, private warn: (message: string) => void) {
    this.client = new TikTokLiveConnection(username, { signApiKey:key, processInitialData:false, enableExtendedGiftInfo:false });
    this.client.on(ControlEvent.CONNECTED, () => { if (!this.stopped) { clearTimeout(this.retry); this.retry = undefined; this.delay = 2000; this.onConnection(true); } });
    this.client.on(WebcastEvent.CHAT, data => {
      if (this.stopped) return;
      const event = normalizeChat(data);
      if (event) this.onComment(event);
    });
    this.client.on(ControlEvent.DISCONNECTED, () => this.schedule());
    this.client.on(WebcastEvent.STREAM_END, () => this.schedule());
    this.client.on(ControlEvent.ERROR, () => { this.warn('TikTok connector error; credentials are omitted from logs'); this.schedule(); });
  }
  private schedule() {
    if (this.stopped || this.resetting) return;
    this.onConnection(false);
    if (this.retry) return;
    this.retry = setTimeout(() => { this.retry = undefined; void this.connect(); }, this.delay);
    this.delay = Math.min(60000, this.delay * 2);
  }
  private async connect() {
    if (this.stopped) return;
    try {
      this.resetting = true;
      try { await this.client.disconnect(); } finally { this.resetting = false; }
      if (!this.stopped) await this.client.connect();
    }
    catch { this.warn('TikTok unavailable: verify LIVE account, username and Euler quota'); this.schedule(); }
  }
  start() { void this.connect(); }
  async stop() { this.stopped = true; clearTimeout(this.retry); await this.client.disconnect(); }
}
