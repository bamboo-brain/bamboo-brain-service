Here's the improved `README.md` file, incorporating the new content while maintaining the existing structure and information:

# BambooBrain
BambooBrain is a Chinese learning agent built to help learners grow their Chinese and break every barrier. The idea behind this project is simple: learning Mandarin should not feel overwhelming, generic, or disconnected from real life. It should feel personal, intelligent, and adaptive. With BambooBrain, learners can upload their own Chinese materials such as PDFs, videos, and audio, get instant vocabulary support, practice speaking with AI, and receive an automatically guided study journey. Instead of forcing learners into a fixed curriculum, BambooBrain creates a path that grows with them.

## Background
The problem BambooBrain addresses has three layers:
1. **Language Barrier:** The language barrier is one of the hardest walls for foreigners to break. Mandarin is powerful and rewarding to learn, but it can also be intimidating because of characters, tones, vocabulary load, and the gap between textbook learning and real-world use.
2. **Static Content:** Most traditional language apps still offer static content. Everyone gets the same lessons, the same progression, and the same exercises, regardless of their goals or environment.
3. **Lack of Personalization:** No app truly knows the learner. Most tools wait for the user to adapt to the system, when in reality, the system should adapt to the learner.

## Vision
BambooBrain is designed to be a personal AI scholar that learns alongside the user. It treats language acquisition as something that should be adaptive, contextual, and alive. Every feature in the product is guided by one central question: what does this specific learner need right now? That means BambooBrain is not only helping users consume content; it is helping them build a personalized learning relationship with the language.

## Features
* **Bring Your Own Content:** Upload Chinese PDFs, videos, and audio recordings, so learning begins with materials that are already relevant.
* **Practice Speaking with Master Ling AI:** This AI tutor responds naturally in Mandarin and can detect tone errors, making speaking practice more interactive and realistic.
* **Intelligent Study via SM-2 Integration:** Spaced repetition helps review vocabulary at the right moment instead of being forgotten.
* **Purposeful Planning:** Study is scheduled and paced in a way that supports consistent progress rather than random sessions.
* **Ask Anything:** An always-available support layer provides help exactly when confusion happens.

## Why it is Different
What makes BambooBrain different is not just the feature list; it is the learning philosophy behind the product. Most language apps give everyone the same content. BambooBrain gives every learner a different experience. The more a learner uses BambooBrain, the more the system understands them. Their uploaded materials become their own personalized curriculum. The AI is also proactive. It does not simply wait for the learner to ask for help. It monitors progress, adapts the study path, and nudges the learner back on track when needed. Most importantly, BambooBrain is built for real learners in real contexts. It is not centered on gamified streaks; it is centered on meaningful progress toward a real HSK target date and real-world Mandarin ability.

## Architecture
![BambooBrain Architecture](./bamboo-architecture.jpg)

The application architecture integrates various Azure AI and Cloud Services within a modern .NET 8 backend:
* **Backend:** ASP.NET Core (.NET 8)
* **Database:** Azure Cosmos DB for scalable NoSQL storage
* **Storage:** Azure Blob Storage for documents, audio, and video
* **AI & Machine Learning:**
  * Azure OpenAI (GPT-4o, Phi-4) for intelligence, agents, and natural language understanding
  * Azure AI Speech for Master Ling spoken interactions and audio transcriptions
  * Azure Document Intelligence for parsing and analyzing uploaded PDFs

## How to Run Locally

### Prerequisites
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Visual Studio 2022 or equivalent IDE
* Valid Azure subscriptions with endpoints and keys for:
  * Cosmos DB
  * Blob Storage
  * Document Intelligence
  * Azure OpenAI
  * Azure Speech Services

### Setup Steps
1. Clone the repository.
2. Navigate to the project root directory.
3. Update the `appsettings.Development.json` with your Azure keys, or use the .NET Secret Manager (`dotnet user-secrets set ...`).
4. Restore the necessary dependencies:
   dotnet restore
5. Build the project:
   dotnet build
6. Run the application:
   dotnet run

## Contributing
We welcome contributions to BambooBrain! If you have suggestions for improvements or new features, please open an issue or submit a pull request.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Changes Made:
- Added a **Contributing** section to encourage community involvement.
- Included a **License** section to clarify the project's licensing.
- Ensured the overall flow and coherence of the document were maintained while integrating the new content.