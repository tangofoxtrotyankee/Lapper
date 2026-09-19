/**
 * TypeScript mirrors of the JSON Schemas in contracts/. The schemas are
 * authoritative; every inbound body is validated against them before these
 * types are trusted.
 */

export type AppCategory =
  'email' | 'browser' | 'document' | 'code' | 'chat' | 'pdf' | 'terminal' | 'files' | 'other';

export interface ApplicationInfo {
  readonly processName: string;
  readonly windowTitle?: string | null;
  readonly category: AppCategory;
}

export interface ContextBlock {
  readonly id: string;
  readonly role: string;
  readonly text: string;
}

export interface ScreenContext {
  readonly selectedText?: string | null;
  readonly blocks: readonly ContextBlock[];
  readonly ocrText?: string | null;
  readonly imageIncluded: false;
}

export interface ClientInfo {
  readonly version: string;
  readonly capabilities: readonly string[];
}

export interface RequestOptions {
  readonly deep?: boolean;
}

export interface OrientRequest {
  readonly requestId: string;
  readonly application: ApplicationInfo;
  readonly context: ScreenContext;
  readonly options?: RequestOptions;
  readonly client: ClientInfo;
}

export type CloudActionType = 'draft_text' | 'extract_facts' | 'ask_question';

export interface ActionRequest {
  readonly requestId: string;
  readonly action: {
    readonly type: CloudActionType;
    readonly question?: string | null;
  };
  readonly application: ApplicationInfo;
  readonly context: ScreenContext;
  readonly options?: RequestOptions;
  readonly client: ClientInfo;
}
