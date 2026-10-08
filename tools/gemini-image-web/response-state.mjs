// 只接收当前模型回复，不能传入整页文本（会包含用户提示词和历史内容）。
export function classifyResponse(text) {
  if (/something went wrong|出了点问题/i.test(text)) return { kind: 'transient-error', message: text.trim() };
  if (/can(?:not|'t|’t) (?:create|generate) (?:the )?image(?:s)?|couldn't generate|unable to create|hard time fulfilling your request|无法完成.*请求|cannot help with|can['’]t help with|against (?:our |the )?(?:content |safety )?polic|violates? (?:our |the )?(?:content |safety )?polic|无法(?:生成|创建).*图|不能(?:生成|创建).*图|违反.*(?:政策|准则)|不符合.*(?:政策|准则)/i.test(text)) return { kind: 'generation-error', message: text.trim() };
  return { kind: 'pending' };
}

// Preserve the provider wording independently of our error summary.
export function responseError(summary, text, kind, conversationUrl) {
  const error = new Error(`${summary}${text.trim() ? `\nGemini 当前回复原文：\n${text.trim()}` : ''}\n对话：${conversationUrl}`);
  error.response_text = text.trim();
  error.response_kind = kind;
  error.conversation_url = conversationUrl;
  return error;
}
