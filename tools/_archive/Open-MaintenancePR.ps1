param(
  [string]$BranchName = "chore/maintenance-ci-hardening",
  [switch]$ApplyToRelease,        # se passato, prova ad applicare i drop-in al release.yml
  [string]$ReleaseWorkflow = ".github/workflows/release.yml",
  [string]$BaseBranch = "main",
  [string]$Title = "chore: maintenance (editorconfig, CODEOWNERS, release hardening)",
  [string]$BodyExtra = ""
)

$ErrorActionPreference = 'Stop'

function Require($cmd){
  if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
    throw "$cmd non trovato. Installalo e riprova."
  }
}

function RepoRoot {
  $p = (git rev-parse --show-toplevel) 2>$null
  if (-not $p) { throw "Non è un repository git valido." }
  return $p.Trim()
}

# Blocchi YAML/PS da inserire (here-string singola quota => nessuna interpolazione)
$ConcurrencyBlock = @'
concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false
'@

$CacheNuGetStep = @'
      - name: Cache NuGet
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/packages.lock.json') }}
          restore-keys: |
            nuget-${{ runner.os }}-
'@

$VerifyTagStep = @'
      - name: Verify tag matches Directory.Build.props <Version>
        shell: pwsh
        run: |
          $tag = $env:GITHUB_REF_NAME
          if ($tag.StartsWith('v')) { $tag = $tag.Substring(1) }
          [xml]$xml = Get-Content -Raw "Directory.Build.props"
          $ver = $xml.Project.PropertyGroup.Version
          if (-not $ver) { throw "<Version> mancante in Directory.Build.props" }
          if ($ver -ne $tag) { throw "Tag v$tag non corrisponde a <Version> $ver" }
          Write-Host "Version check OK: v$tag"
'@

$EnsureNupkgStep = @'
      - name: Ensure packages exist
        shell: bash
        run: |
          set -euo pipefail
          ls -l ./nupkg || true
          count=$(ls -1 ./nupkg/*.nupkg 2>/dev/null | wc -l | tr -d ' ')
          if [ "$count" -eq 0 ]; then
            echo "::error ::Nessun .nupkg trovato in ./nupkg"; exit 1
          fi
'@

function Insert-AfterFirstMatch {
  param([string]$Text, [string]$Pattern, [string]$Block)
  $m = [regex]::Match($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
  if ($m.Success) {
    return $Text.Insert($m.Index + $m.Length, "`r`n`r`n$Block`r`n")
  } else {
    return "$Block`r`n`r`n$Text"
  }
}

function Ensure-CheckoutFetchDepth0 {
  param([string]$Yaml)
  # Inserisce "with: fetch-depth: 0" nel PRIMO step di checkout se non presente subito dopo "uses:"
  $rx = [regex]'(?ms)^(?<head>\s*-\s*name:\s*.*?\r?\n\s*uses:\s*actions/checkout@[^ \r\n]+[^\r\n]*\r?\n)(?<rest>(?!\s*with:).*)'
  $m = $rx.Match($Yaml)
  if ($m.Success) {
    $head = $m.Groups['head'].Value
    if ($head -notmatch '(?m)^\s*with:\s*$') {
      $injected = $head + "      with:`r`n        fetch-depth: 0`r`n"
      return $Yaml.Substring(0, $m.Groups['head'].Index) + $injected + $Yaml.Substring($m.Groups['head'].Index + $m.Groups['head'].Length)
    }
  }
  return $Yaml
}

# --- prerequisites
Require git
Require gh

$root = RepoRoot
Set-Location $root

# --- checkout base
git fetch origin $BaseBranch --prune
git checkout $BaseBranch
git pull --ff-only

# --- create branch
git checkout -b $BranchName

# --- write .editorconfig (idempotente)
$editorconfigPath = Join-Path $root ".editorconfig"
$editorconfigContent = @'
root = true

[*.{cs,csx}]
charset = utf-8-bom
end_of_line = crlf
insert_final_newline = true
indent_style = space
indent_size = 2
trim_trailing_whitespace = true

# C#
dotnet_style_qualification_for_field = true:suggestion
dotnet_style_qualification_for_property = true:suggestion
dotnet_style_qualification_for_method = true:suggestion
dotnet_style_null_propagation = true:suggestion
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:suggestion

csharp_new_line_before_open_brace = all
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# Analyzer severities
dotnet_analyzer_diagnostic.category-Style.severity = suggestion
dotnet_analyzer_diagnostic.category-Design.severity = warning
dotnet_analyzer_diagnostic.category-Security.severity = warning
'@
Set-Content -Path $editorconfigPath -Value $editorconfigContent -Encoding UTF8 -NoNewline

# --- write CODEOWNERS
$codeownersDir = Join-Path $root ".github"
if (-not (Test-Path $codeownersDir)) { New-Item -ItemType Directory -Path $codeownersDir | Out-Null }
$codeownersPath = Join-Path $codeownersDir "CODEOWNERS"
$codeownersContent = @'
# Revisori default
* @GianlucaProgesi

# Aree
/src/ProgesiCore/ @GianlucaProgesi
/src/ProgesiRepositories.* @GianlucaProgesi
/tests/ @GianlucaProgesi
/.github/ @GianlucaProgesi
'@
Set-Content -Path $codeownersPath -Value $codeownersContent -Encoding UTF8 -NoNewline

# --- (opzionale) applica hardening su release.yml
$changesApplied = $false
if ($ApplyToRelease -and (Test-Path $ReleaseWorkflow)) {
  $yml = Get-Content -Raw $ReleaseWorkflow

  # 1) concurrency root-level
  if ($yml -notmatch "(?m)^\s*concurrency:") {
    # inserisci dopo 'permissions:' se presente, altrimenti dopo 'on:' o dopo 'name:' o in testa
    if ($yml -match "(?ms)^\s*permissions:.*?$") {
      $yml = Insert-AfterFirstMatch -Text $yml -Pattern "^\s*permissions:.*?$" -Block $ConcurrencyBlock
    }
    elseif ($yml -match "(?ms)^\s*on:.*?$") {
      $yml = Insert-AfterFirstMatch -Text $yml -Pattern "^\s*on:.*?$" -Block $ConcurrencyBlock
    }
    elseif ($yml -match "(?ms)^\s*name:.*?$") {
      $yml = Insert-AfterFirstMatch -Text $yml -Pattern "^\s*name:.*?$" -Block $ConcurrencyBlock
    }
    else {
      $yml = "$ConcurrencyBlock`r`n`r`n$yml"
    }
    $changesApplied = $true
  }

  # 2) checkout fetch-depth:0
  if ($yml -match "actions/checkout@") {
    $new = Ensure-CheckoutFetchDepth0 -Yaml $yml
    if ($new -ne $yml) { $yml = $new; $changesApplied = $true }
  }

  # 3) Cache NuGet dopo setup-dotnet se manca
  if ($yml -notmatch "actions/cache@v4" -and $yml -match "actions/setup-dotnet@v4") {
    $yml = [regex]::Replace(
      $yml,
      "(?ms)(-\s*name:\s*Setup.*?actions/setup-dotnet@v4.*?\r?\n)",
      '$1' + $CacheNuGetStep
    )
    $changesApplied = $true
  }

  # 4) Verifica tag==Version prima del primo 'dotnet pack'
  if ($yml -match "dotnet pack" -and $yml -notmatch "Verify tag matches Directory.Build.props <Version>") {
    $yml = [regex]::Replace(
      $yml,
      "(?ms)(\r?\n\s*-\s*name:\s*.*?dotnet pack.*?$)",
      "`r`n$VerifyTagStep" + '$1'
    )
    $changesApplied = $true
  }

  # 5) Ensure packages exist dopo l'ultimo 'dotnet pack'
  if ($yml -match "dotnet pack" -and $yml -notmatch "Ensure packages exist") {
    # append il blocco Ensure subito dopo l'ULTIMO pack
    $lastPack = [regex]::Matches($yml, "(?ms)-\s*name:\s*.*?dotnet pack.*?$")
    if ($lastPack.Count -gt 0) {
      $m = $lastPack[$lastPack.Count - 1]
      $yml = $yml.Insert($m.Index + $m.Length, "`r`n$EnsureNupkgStep")
      $changesApplied = $true
    }
  }

  if ($changesApplied) {
    Set-Content -Path $ReleaseWorkflow -Value $yml -Encoding UTF8
  }
}

# --- commit & push
git add -A
if (git diff --cached --quiet) {
  Write-Host "Nessuna modifica da commitare." -ForegroundColor Yellow
} else {
  git commit -m $Title
}
git push -u origin $BranchName

# --- open PR
$body = @"
Questa PR aggiunge:

- **.editorconfig** (root) per stile coerente
- **.github/CODEOWNERS** per routing review
- (Opzionale) hardening del workflow **release**:
  - \`concurrency\` a livello root (evita run concorrenti sullo stesso tag)
  - \`checkout\` con \`fetch-depth: 0\` (SourceLink/changelog)
  - Cache NuGet
  - Verifica \`tag == <Version>\` in \`Directory.Build.props\`
  - Check presenza .nupkg dopo il pack

$BodyExtra

> Nota: lo script è idempotente e **non** rimuove nulla dal tuo workflow.
"@

gh pr create --base $BaseBranch --head $BranchName --title $Title --body $body
Write-Host "PR aperta."
