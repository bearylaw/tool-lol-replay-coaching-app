# Golden path: install, dev, format, lint, typecheck, test, check, build.
# Recipes run in PowerShell 7 so they behave the same in CI and locally.
set windows-shell := ["pwsh", "-NoLogo", "-NoProfile", "-Command"]
set shell := ["pwsh", "-NoLogo", "-NoProfile", "-Command"]

solution := "RiftReview.slnx"
app := "src/RiftReview.App/RiftReview.App.csproj"

# Restore exactly what the lockfiles pin.
install:
    dotnet restore {{solution}} --locked-mode

# Start the app for local work.
dev:
    dotnet run --project {{app}}

# Rewrite C# layout with the formatter.
format:
    dotnet format whitespace {{solution}}

# Compile with every analyzer and style rule; any warning fails.
lint:
    dotnet build {{solution}} --no-restore -c Debug -warnaserror -nologo -clp:ErrorsOnly

# The compiler is the type checker in C#; same build as lint.
typecheck: lint

# Run every test project, failures only.
test:
    dotnet test --solution {{solution}} --no-restore --no-build -c Debug --output Normal --no-progress

# Read every replay in a folder with the real parser, after a new patch.
verify-replays folder:
    dotnet build src/RiftReview.Core.Tests -nologo -clp:ErrorsOnly
    dotnet build src/RiftReview.Timeline.Tests -nologo -clp:ErrorsOnly
    $env:RIFTREVIEW_REPLAY_DIR = '{{folder}}'; src/RiftReview.Core.Tests/bin/Debug/net10.0/RiftReview.Core.Tests.exe -method '*ReadsEveryReplayInTheConfiguredFolder' -diagnostics
    $env:RIFTREVIEW_REPLAY_DIR = '{{folder}}'; src/RiftReview.Timeline.Tests/bin/Debug/net10.0/RiftReview.Timeline.Tests.exe -method '*DeathsAndLevelUpsMatchTheEndOfGameStats' -method '*TurretFallsMatchEachTeamsTurretsLost' -method '*KillsTakedownsAndObjectivesMatchTheEndOfGameStats' -diagnostics

# What CI runs: format check, analyzer build, tests.
check:
    dotnet format whitespace {{solution}} --verify-no-changes
    just lint
    just test

# Produce dist/RiftReview.exe, a single self-contained file.
build:
    if (Test-Path dist) { Get-ChildItem dist -File | Remove-Item -Force }
    dotnet publish {{app}} -c Release -o dist --nologo
