namespace common;

public record PromptRequest(string prompt, int max_tokens = 100, double temperature = 0.3);
public record CompletionResponse(string content);
