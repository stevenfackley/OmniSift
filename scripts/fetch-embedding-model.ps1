<#
.SYNOPSIS
  Downloads the local ONNX embedding model (bge-small-en-v1.5) used when
  Embedding:Provider = Onnx. Idempotent — skips files already present.

.DESCRIPTION
  The model binary (~130 MB) is intentionally NOT committed to git (see
  .gitignore: /models/). Run this once on each dev machine / build host that
  serves the ONNX provider. Sources:
    - ONNX graph : Xenova/bge-small-en-v1.5 (onnx/model.onnx, fp32, last_hidden_state output)
    - vocab.txt  : BAAI/bge-small-en-v1.5 (BERT WordPiece, consumed by Microsoft.ML.Tokenizers BertTokenizer)
    - pooling    : BAAI/bge-small-en-v1.5/1_Pooling/config.json — provenance: pooling_mode_cls_token = true

  Pooling for this model is CLS (not mean). Keep Embedding:Pooling = Cls.

.PARAMETER OutDir
  Target directory. Defaults to <repoRoot>/models/bge-small-en-v1.5.
#>
[CmdletBinding()]
param(
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'models/bge-small-en-v1.5' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$files = @(
    @{ Name = 'model.onnx';   Url = 'https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/onnx/model.onnx' },
    @{ Name = 'vocab.txt';    Url = 'https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/vocab.txt' },
    @{ Name = 'tokenizer.json'; Url = 'https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/tokenizer.json' },
    @{ Name = 'pooling.json'; Url = 'https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/1_Pooling/config.json' }
)

foreach ($f in $files) {
    $dest = Join-Path $OutDir $f.Name
    if ((Test-Path $dest) -and ((Get-Item $dest).Length -gt 0)) {
        Write-Host "[skip] $($f.Name) already present ($([math]::Round((Get-Item $dest).Length / 1MB, 1)) MB)"
        continue
    }
    Write-Host "[get ] $($f.Name) <- $($f.Url)"
    Invoke-WebRequest -Uri $f.Url -OutFile $dest -UseBasicParsing
    Write-Host "[ok  ] $($f.Name) ($([math]::Round((Get-Item $dest).Length / 1MB, 1)) MB)"
}

Write-Host ""
Write-Host "Model ready at: $OutDir"
Write-Host "Set Embedding:Provider=Onnx and Embedding:ModelPath / Embedding:TokenizerPath to point here."
