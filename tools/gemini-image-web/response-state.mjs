// 只接收当前模型回复，不能传入整页文本（会包含用户提示词和历史内容）。
export function classifyResponse(text) {
  if (isQuotaNotice(text)) return { kind: 'quota', message: text.trim() };
  if (/something went wrong|I encountered an error doing what you asked|出了点问题/i.test(text)) return { kind: 'transient-error', message: text.trim() };
  if (/can(?:not|'t|’t) (?:create|generate) (?:the )?image(?:s)?|couldn't generate|unable to create|hard time fulfilling your request|无法完成.*请求|cannot help with|can['’]t help with|against (?:our |the )?(?:content |safety )?polic|violates? (?:our |the )?(?:content |safety )?polic|无法(?:生成|创建).*图|不能(?:生成|创建).*图|违反.*(?:政策|准则)|不符合.*(?:政策|准则)/i.test(text)) return { kind: 'generation-error', message: text.trim() };
  return { kind: 'pending' };
}

export function isQuotaNotice(text) {
  return /\b(?:daily\s+|image generation\s+)?limit reached\b|\byou(?:'|’)ve reached (?:your\s+)?(?:daily\s+|image generation\s+)?limit\b|(?:已达到|已用尽|已超出).{0,16}(?:额度|上限)|(?:额度|上限).{0,16}(?:已达到|已用尽|已超出)/i.test(text);
}

export function quotaError(text, pageUrl) {
  const notice = text.trim();
  const error = new Error(`Gemini 图片生成额度已达上限，未继续提交或重试。\n页面提示：${notice}\n页面：${pageUrl}`);
  error.response_text = notice;
  error.response_kind = 'quota';
  error.conversation_url = pageUrl;
  return error;
}

// Preserve the provider wording independently of our error summary.
export function responseError(summary, text, kind, conversationUrl) {
  const error = new Error(`${summary}${text.trim() ? `\nGemini 当前回复原文：\n${text.trim()}` : ''}\n对话：${conversationUrl}`);
  error.response_text = text.trim();
  error.response_kind = kind;
  error.conversation_url = conversationUrl;
  return error;
}
