param(
    [string]$BranchName = "tests/coverage-boost"
)

Write-Host "➡️ Switching to new branch: $BranchName"
git checkout -b $BranchName

Write-Host "➡️ Staging updated test files..."
git add tests/ProgesiCore.Tests/ProgesiMetadataMoreTests.cs
git add tests/ProgesiCore.Tests/ProgesiSnipMoreTests.cs
git add tests/ProgesiCore.Tests/ValueObjectCompareMoreTests.cs

Write-Host "➡️ Committing..."
git commit -m "tests: add Metadata/Snip/ValueObject comparison tests to improve coverage"

Write-Host "➡️ Pushing branch to origin..."
git push -u origin $BranchName

Write-Host "✅ Done. Open a PR on GitHub to merge '$BranchName' into main."
