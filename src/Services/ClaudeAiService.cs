using System.Text;
using System.Text.Json;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Services;

public class ClaudeAiService : IAiWorksheetService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaudeAiService> _logger;

    public ClaudeAiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ClaudeAiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Worksheet> GenerateWorksheetAsync(
        string pdfContent,
        string sourceFileName,
        WorksheetTemplate? template = null)
    {
        var apiKey = _configuration["Claude:ApiKey"]
            ?? throw new InvalidOperationException("Claude API key not configured. Set Claude:ApiKey in appsettings.json.");

        var model     = _configuration["Claude:Model"]     ?? "claude-sonnet-4-6";
        var maxTokens = int.Parse(_configuration["Claude:MaxTokens"] ?? "4096");

        var prompt = BuildPrompt(pdfContent, template);

        var requestBody = new
        {
            model,
            max_tokens = maxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json    = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        client.Timeout = TimeSpan.FromSeconds(120);

        _logger.LogInformation("Calling Claude API with model {Model} (template: {Template}) to generate worksheet",
            model, template?.Name ?? "none");

        var response     = await client.PostAsync("https://api.anthropic.com/v1/messages", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error {StatusCode}: {Body}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Claude API returned {response.StatusCode}. Check your API key and quota.");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var textContent = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()
            ?? throw new Exception("Empty response from Claude API");

        _logger.LogInformation("Received Claude response, parsing worksheet JSON");

        var worksheetJson = ExtractJson(textContent);

        var worksheet = JsonSerializer.Deserialize<Worksheet>(worksheetJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Exception("Failed to deserialize worksheet JSON from Claude response");

        worksheet.Id             = Guid.NewGuid().ToString();
        worksheet.SourceFileName = sourceFileName;
        worksheet.GeneratedAt    = DateTime.Now;

        _logger.LogInformation("Generated worksheet '{Title}' with {Count} questions via Claude",
            worksheet.Title, worksheet.Questions.Count);

        return worksheet;
    }

    private static string BuildPrompt(string pdfContent, WorksheetTemplate? template)
    {
        const int maxLen  = 8000;
        var truncated = pdfContent.Length > maxLen
            ? pdfContent[..maxLen] + "\n\n[Content truncated for length]"
            : pdfContent;

        // Build the question-type rules section depending on whether a template is active
        string questionRules;
        if (template != null && template.QuestionTypes.Any())
        {
            var lines = template.QuestionTypes
                .Select(qt => $"  - {qt.Count} question(s) of type \"{qt.Type}\" — {qt.Points} points each")
                .ToList();
            var typeList = string.Join("\n", lines);
            var total    = template.TotalQuestions;

            questionRules = $"""
                TEMPLATE OVERRIDE — follow these rules exactly:
                Generate exactly {total} question(s) using the following distribution:
                {typeList}

                Do NOT add any other question types beyond those listed.
                Assign points exactly as specified per type above.
                """;

            if (!string.IsNullOrWhiteSpace(template.SpecialInstructions))
                questionRules += $"\n\nAdditional AI instructions from template:\n{template.SpecialInstructions}";
        }
        else
        {
            questionRules = """
                DEFAULT RULES:
                - Generate 12–15 questions total
                - ALWAYS include: at least 4 multiple_choice, 2 true_false, 2 fill_blank, 1 short_answer
                - MATHEMATICS / SCIENCE CONTENT: include at least 3 word_problem questions requiring step-by-step calculation
                - OTHER SUBJECTS: include 1–2 word_problem only if they fit naturally
                - Points: multiple_choice=5, true_false=2, fill_blank=3, short_answer=10, word_problem=15
                - Vary difficulty: at least 3 easy, 5 medium, 3 challenging
                """;
        }

        // Difficulty instruction (applies whether template is set or not)
        var difficulty = template?.Difficulty ?? "mixed";
        var difficultyRule = difficulty switch
        {
            "easy"   => "DIFFICULTY: All questions must be EASY — suitable for beginners. Focus on direct recall, simple definitions, and basic comprehension. Avoid multi-step reasoning or analysis.",
            "medium" => "DIFFICULTY: All questions must be MEDIUM — require some understanding and application beyond simple recall. Students should need to think, but not deeply analyse or synthesise.",
            "hard"   => "DIFFICULTY: All questions must be HARD — require critical thinking, synthesis, and complex multi-step reasoning. Avoid trivial recall questions entirely.",
            _        => "DIFFICULTY: Use a MIXED spread — approximately 30% easy, 50% medium, 20% hard across all questions."
        };

        return $$"""
            You are an expert educator creating a student worksheet from the following educational PDF content.

            Return ONLY a single valid JSON object — no markdown fences, no explanation, nothing else before or after.
            Use this exact schema (five question types available):

            {
              "title": "Descriptive worksheet title",
              "subject": "Subject area (e.g. Biology, History, Mathematics)",
              "topic": "Specific topic from the content",
              "instructions": "Read each question carefully and answer to the best of your ability. Show all working for word problems.",
              "estimatedMinutes": 35,
              "questions": [
                {
                  "id": 1,
                  "type": "multiple_choice",
                  "text": "Question text",
                  "options": ["A. First option", "B. Second option", "C. Third option", "D. Fourth option"],
                  "answer": "A. First option",
                  "points": 5
                },
                {
                  "id": 2,
                  "type": "true_false",
                  "text": "Statement to evaluate",
                  "options": ["True", "False"],
                  "answer": "True",
                  "points": 2
                },
                {
                  "id": 3,
                  "type": "fill_blank",
                  "text": "The _____ process converts sunlight into energy.",
                  "options": [],
                  "answer": "photosynthesis",
                  "points": 3
                },
                {
                  "id": 4,
                  "type": "short_answer",
                  "text": "In 2-3 sentences, explain...",
                  "options": [],
                  "answer": "Model answer: A complete answer should mention...",
                  "points": 10
                },
                {
                  "id": 5,
                  "type": "word_problem",
                  "text": "A train travels at 60 km/h for 2.5 hours. How far does it travel? Show your working.",
                  "options": [],
                  "answer": "Step 1: Distance = Speed × Time\nStep 2: Distance = 60 × 2.5\nAnswer: 150 km",
                  "points": 15
                }
              ]
            }

            {{questionRules}}

            {{difficultyRule}}

            General rules (always apply):
            - Base every question directly on the PDF content
            - Use exactly _____ (5 underscores) for blanks in fill_blank questions
            - word_problem answers MUST include line-by-line working using \n between steps (Step 1:, Step 2:, Answer:)
            - Return ONLY the JSON object

            PDF Content:
            {{truncated}}
            """;
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }
}
