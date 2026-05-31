using System.Text;
using System.Text.Json;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Services;

public class OpenAiService : IAiWorksheetService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Worksheet> GenerateWorksheetAsync(
        string             pdfContent,
        string             sourceFileName,
        WorksheetTemplate? template        = null,
        string?            sampleQuestions = null,
        byte[]?            imageBytes      = null,
        string?            imageMimeType   = null)
    {
        var apiKey = _configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI API key not configured. Set OpenAI:ApiKey in appsettings.json.");

        bool isImageMode = imageBytes != null && imageBytes.Length > 0;

        string model;
        int    maxTokens;

        var systemMessage = new { role = "system", content = """
You are an expert educational worksheet designer with deep knowledge of pedagogy across all school subjects and grade levels.

Your job is to generate ORIGINAL student worksheets. You have two modes depending on what the user provides:
- STUDY MATERIAL (textbook, notes, article, image of a textbook page): extract key concepts and create questions that test understanding of those concepts.
- EXISTING WORKSHEET (already has questions, blanks, problems): analyze the question patterns, topic, difficulty, and format — then generate a completely NEW worksheet on the same topic. Never reproduce or paraphrase questions from the source.

You always:
- Produce well-structured, pedagogically sound questions appropriate to the topic and difficulty level
- Vary cognitive demand: mix recall, comprehension, application, and analysis
- Return ONLY a single valid JSON object — no markdown fences, no prose, no commentary before or after
- Never include questions copied or closely paraphrased from the source content
""" };

        object userMessage;
        if (isImageMode)
        {
            var visionModel = _configuration["OpenAI:VisionModel"]
                ?? throw new InvalidOperationException("OpenAI:VisionModel is not configured. Add it to appsettings.json.");
            model     = visionModel;
            maxTokens = int.Parse(_configuration["OpenAI:VisionMaxTokens"] ?? "4096");

            var base64Image = Convert.ToBase64String(imageBytes!);
            var mimeType    = imageMimeType ?? "image/jpeg";
            var textPrompt  = BuildPrompt(string.Empty, template, sampleQuestions, isImageMode: true);

            // Vision API: content is an array of text + image parts
            userMessage = new
            {
                role    = "user",
                content = new object[]
                {
                    new { type = "text",      text      = textPrompt },
                    new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                }
            };
        }
        else
        {
            model     = _configuration["OpenAI:Model"] ?? "gpt-4o";
            maxTokens = int.Parse(_configuration["OpenAI:MaxTokens"] ?? "0");

            var textPrompt = BuildPrompt(pdfContent, template, sampleQuestions);
            userMessage = new { role = "user", content = textPrompt };
        }

        var requestBody = new Dictionary<string, object>
        {
            ["model"]    = model,
            ["messages"] = new object[] { systemMessage, userMessage }
        };

        if (maxTokens > 0)
            requestBody["max_tokens"] = maxTokens;

        var json    = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        client.Timeout = TimeSpan.FromSeconds(120);

        _logger.LogInformation(
            "Calling OpenAI API with model {Model} (template: {Template}, samples: {HasSamples}, mode: {Mode})",
            model, template?.Name ?? "none", sampleQuestions != null ? "yes" : "no",
            isImageMode ? "vision" : "text");

        var response     = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error {StatusCode}: {Body}", response.StatusCode, responseJson);
            throw new HttpRequestException($"OpenAI API returned {response.StatusCode}. Check your API key and quota.");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var textContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? throw new Exception("Empty response from OpenAI API");

        _logger.LogInformation("Received OpenAI response, parsing worksheet JSON");

        var worksheetJson = ExtractJson(textContent);

        var worksheet = JsonSerializer.Deserialize<Worksheet>(worksheetJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Exception("Failed to deserialize worksheet JSON from OpenAI response");

        worksheet.Id             = Guid.NewGuid().ToString();
        worksheet.SourceFileName = sourceFileName;
        worksheet.GeneratedAt    = DateTime.Now;

        _logger.LogInformation("Generated worksheet '{Title}' with {Count} questions via OpenAI",
            worksheet.Title, worksheet.Questions.Count);

        return worksheet;
    }

    private static string BuildPrompt(string pdfContent, WorksheetTemplate? template, string? sampleQuestions = null, bool isImageMode = false)
    {
        var questionRules = BuildQuestionRules(template);
        var difficultyRule = BuildDifficultyRule(template?.Difficulty ?? "mixed");
        var sampleSection  = BuildSampleSection(sampleQuestions);

        var contentSection = isImageMode
            ? "The image attached above is the source material. Read all visible text and content from it carefully."
            : $"""
            Content:
            {TruncateContent(pdfContent)}
            """;

        var inputDescription = isImageMode
            ? "a photo or scanned image"
            : "the provided content";

        return $$"""
            Your task is to generate a BRAND NEW student worksheet based on {{inputDescription}}.

            STEP 1 — CLASSIFY THE INPUT:
            Determine which type it is:
            (A) STUDY MATERIAL — a textbook chapter, article, notes, or reference content with facts/concepts to learn.
            (B) EXISTING WORKSHEET — content that already contains questions, exercises, problems, blanks, answer keys, or numbered items that students answer.

            STEP 2 — EXTRACT PATTERNS (critical when input is type B):
            If the input is an EXISTING WORKSHEET, do NOT reproduce or rephrase any of its questions.
            Instead, analyze and extract only the PEDAGOGICAL PATTERNS:
            - Subject and topic domain (e.g., Grade 5 fractions, photosynthesis, World War II causes)
            - Question type mix (e.g., how many multiple choice, fill-in-the-blank, word problems)
            - Difficulty level and style (e.g., recall vs. application, number ranges used in math)
            - Format conventions (e.g., "Show your working", diagram labels, matching columns)
            Then use those patterns as a blueprint to create entirely new questions on the SAME TOPIC.

            If the input is STUDY MATERIAL, generate questions that test the key concepts, facts, and reasoning it covers.

            STEP 3 — GENERATE FRESH QUESTIONS:
            Create completely original questions. Every question must be new — different wording, different numbers, different scenarios, different answer choices from anything in the source.

            Return ONLY a single valid JSON object — no markdown fences, no explanation, nothing else.
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
            {{sampleSection}}
            General rules (always apply):
            - NEVER copy, quote, or rephrase questions that already appear in the source or sample questions
            - Every question must be original — new wording, new numbers, new scenarios
            - Use exactly _____ (5 underscores) for blanks in fill_blank questions
            - word_problem answers MUST include line-by-line working using \n between steps (Step 1:, Step 2:, Answer:)
            - Return ONLY the JSON object

            {{contentSection}}
            """;
    }

    private static string BuildQuestionRules(WorksheetTemplate? template)
    {
        if (template != null && template.QuestionTypes.Any())
        {
            var lines    = template.QuestionTypes.Select(qt => $"  - {qt.Count} question(s) of type \"{qt.Type}\" — {qt.Points} points each");
            var typeList = string.Join("\n", lines);
            var total    = template.TotalQuestions;
            var rules    = $"""
                TEMPLATE OVERRIDE — follow these rules exactly:
                Generate exactly {total} question(s) using the following distribution:
                {typeList}

                Do NOT add any other question types beyond those listed.
                Assign points exactly as specified per type above.
                """;
            if (!string.IsNullOrWhiteSpace(template.SpecialInstructions))
                rules += $"\n\nAdditional AI instructions from template:\n{template.SpecialInstructions}";
            return rules;
        }

        return """
            DEFAULT RULES:
            - Generate 12–15 questions total
            - ALWAYS include: at least 4 multiple_choice, 2 true_false, 2 fill_blank, 1 short_answer
            - MATHEMATICS / SCIENCE CONTENT: include at least 3 word_problem questions requiring step-by-step calculation
            - OTHER SUBJECTS: include 1–2 word_problem only if they fit naturally
            - Points: multiple_choice=5, true_false=2, fill_blank=3, short_answer=10, word_problem=15
            - Vary difficulty: at least 3 easy, 5 medium, 3 challenging
            """;
    }

    private static string BuildDifficultyRule(string difficulty) => difficulty switch
    {
        "easy"   => "DIFFICULTY: All questions must be EASY — suitable for beginners. Focus on direct recall, simple definitions, and basic comprehension. Avoid multi-step reasoning or analysis.",
        "medium" => "DIFFICULTY: All questions must be MEDIUM — require some understanding and application beyond simple recall. Students should need to think, but not deeply analyse or synthesise.",
        "hard"   => "DIFFICULTY: All questions must be HARD — require critical thinking, synthesis, and complex multi-step reasoning. Avoid trivial recall questions entirely.",
        _        => "DIFFICULTY: Use a MIXED spread — approximately 30% easy, 50% medium, 20% hard across all questions."
    };

    private static string BuildSampleSection(string? sampleQuestions)
    {
        if (string.IsNullOrWhiteSpace(sampleQuestions)) return string.Empty;
        return $"""

            SAMPLE QUESTIONS — STYLE GUIDE (critical):
            The user has provided the following sample questions as a style reference.
            Analyse them carefully and match their:
            - Cognitive level (recall, application, analysis, etc.)
            - Sentence structure and phrasing style
            - Difficulty and complexity
            - Question type mix and format conventions
            Generate NEW questions in the same style but on the topic from the source. Do NOT reuse any sample question.

            Sample questions:
            {sampleQuestions}

            """;
    }

    private static string TruncateContent(string text, int maxLen = 8000) =>
        text.Length > maxLen ? text[..maxLen] + "\n\n[Content truncated for length]" : text;

    /// <summary>
    private static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }
}
