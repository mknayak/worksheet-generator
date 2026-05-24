# Worksheet Generator

An ASP.NET Core 8 web application that turns any educational PDF into a fully formatted student worksheet — powered by Claude AI or OpenAI (your choice per generation).

---

## Features

- **PDF Upload** — drag-and-drop or browse; extracts text with PdfPig
- **Dual AI providers** — pick Claude or OpenAI per generation; both produce identical output format
- **Four question types** — Multiple Choice, True/False, Fill in the Blank, Short Answer
- **Local JSON storage** — every worksheet saved as a JSON file under `wwwroot/data/worksheets/`
- **Worksheet viewer** — full HTML preview with answers shown inline
- **Download Worksheet PDF** — clean A4 PDF via html2pdf.js (no answers)
- **Download Answer Key PDF** — separate teacher copy with all answers highlighted
- **Download raw JSON** — for external processing or archiving
- **Student Profiles** — reusable name/class/grade/school info pre-fills worksheet headers
- **History page** — browse, view, download or delete all past worksheets

---

## Quick Start

### 1. Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Claude API key from [console.anthropic.com](https://console.anthropic.com) and/or an OpenAI key from [platform.openai.com](https://platform.openai.com)

### 2. Configure your API keys

Open `appsettings.json` and fill in whichever providers you want to use:

```json
{
  "Claude": {
    "ApiKey": "YOUR_CLAUDE_API_KEY_HERE",
    "Model": "claude-sonnet-4-6",
    "MaxTokens": 4096
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
    "Model": "gpt-4o",
    "MaxTokens": 4096
  }
}
```

You only need to fill in the provider(s) you plan to use. Alternatively use environment variables:

```bash
export Claude__ApiKey="sk-ant-..."
export OpenAI__ApiKey="sk-..."
```

### 3. Run

```bash
cd WorksheetGenerator
dotnet run
```

Then open **http://localhost:5000** in your browser.

---

## Project Structure

```
WorksheetGenerator/
├── Controllers/
│   ├── HomeController.cs          # Upload page + recent worksheets
│   ├── ProfileController.cs       # Student profile CRUD
│   └── WorksheetController.cs     # Generate, view, print, download, delete
│
├── Models/
│   ├── StudentProfile.cs          # Name, class, grade, school, teacher
│   ├── Worksheet.cs               # Worksheet + WorksheetQuestion models
│   └── ViewModels.cs              # View-specific DTOs
│
├── Services/
│   ├── PdfExtractionService.cs    # Text extraction via UglyToad.PdfPig
│   ├── ClaudeAiService.cs         # Claude API integration (HttpClient)
│   ├── WorksheetStorageService.cs # Read/write worksheet JSON files
│   └── ProfileStorageService.cs  # Read/write student profile JSON files
│
├── Views/
│   ├── Home/Index.cshtml          # Upload form + drag-and-drop + recent list
│   ├── Profile/                   # Index, Create, Edit views
│   └── Worksheet/
│       ├── View.cshtml            # Full worksheet preview with answers
│       ├── PrintWorksheet.cshtml  # Clean A4 — questions only (PDF download)
│       ├── PrintAnswers.cshtml    # Clean A4 — answer key (PDF download)
│       └── History.cshtml         # All worksheets table
│
├── wwwroot/data/
│   ├── profiles/                  # Student profiles stored as JSON
│   └── worksheets/                # Generated worksheets stored as JSON
│
├── appsettings.json               # API key + model config
└── Program.cs                     # App bootstrap + DI registration
```

---

## Workflow

```
1. Create a Student Profile (optional but recommended)
        ↓
2. Upload a PDF on the Home page
        ↓
3. App extracts text via PdfPig
        ↓
4. Claude AI generates 12–15 questions → returns JSON
        ↓
5. JSON saved to wwwroot/data/worksheets/{id}.json
        ↓
6. Redirected to Worksheet Viewer
        ↓
7. Download Worksheet PDF  ← student copy (no answers)
   Download Answer Key PDF ← teacher copy (answers highlighted)
   Download JSON           ← raw data
```

---

## Worksheet JSON Format

Each saved worksheet follows this schema:

```json
{
  "id": "uuid",
  "title": "Photosynthesis Worksheet",
  "subject": "Biology",
  "topic": "Photosynthesis and Cellular Respiration",
  "instructions": "Read each question carefully...",
  "estimatedMinutes": 30,
  "studentProfileId": "uuid-or-empty",
  "sourceFileName": "chapter3.pdf",
  "generatedAt": "2026-05-23T10:00:00",
  "questions": [
    {
      "id": 1,
      "type": "multiple_choice",
      "text": "Which organelle carries out photosynthesis?",
      "options": ["A. Mitochondria", "B. Chloroplast", "C. Nucleus", "D. Ribosome"],
      "answer": "B. Chloroplast",
      "points": 5
    },
    {
      "id": 2,
      "type": "true_false",
      "text": "Photosynthesis produces oxygen as a by-product.",
      "options": ["True", "False"],
      "answer": "True",
      "points": 2
    },
    {
      "id": 3,
      "type": "fill_blank",
      "text": "The green pigment in plants is called _____.",
      "options": [],
      "answer": "chlorophyll",
      "points": 3
    },
    {
      "id": 4,
      "type": "short_answer",
      "text": "Explain the role of sunlight in photosynthesis.",
      "options": [],
      "answer": "Model answer: Sunlight provides the energy...",
      "points": 10
    }
  ]
}
```

---

## PDF Tips

For best results upload PDFs that:
- Are **text-based** (not scanned images — those have no extractable text)
- Contain **learning objectives**, **topic headings**, or **key vocabulary**
- Include **sample questions** if available — AI uses them as style guidance
- Are under **20 MB**

---

## Dependencies

| Package | Purpose |
|---|---|
| `UglyToad.PdfPig 0.1.8` | Server-side PDF text extraction |
| Bootstrap 5.3 (CDN) | UI framework |
| Bootstrap Icons (CDN) | Icon set |
| html2pdf.js 0.10.1 (CDN) | Client-side PDF generation |
| Claude API (`claude-sonnet-4-6`) | AI worksheet generation (provider 1) |
| OpenAI API (`gpt-4o`) | AI worksheet generation (provider 2) |

---

## Configuration Reference

| Key | Default | Description |
|---|---|---|
| `Claude:ApiKey` | *(required for Claude)* | Your Anthropic API key |
| `Claude:Model` | `claude-sonnet-4-6` | Claude model to use |
| `Claude:MaxTokens` | `4096` | Max tokens for Claude responses |
| `OpenAI:ApiKey` | *(required for OpenAI)* | Your OpenAI API key |
| `OpenAI:Model` | `gpt-4o` | OpenAI model to use (`gpt-4o`, `gpt-4-turbo`, etc.) |
| `OpenAI:MaxTokens` | `4096` | Max tokens for OpenAI responses |
