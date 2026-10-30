# Componentes de terceiros

O repositório inicial distribui código-fonte e avisos, sem binários de FFmpeg ou .NET.
A licença PolyForm Noncommercial do código original não se aplica aos componentes abaixo.
Cada componente mantém os direitos e condições de sua própria licença.

This source-only repository does not bundle FFmpeg or .NET binaries. The project's
noncommercial license does not restrict third-party components, which retain their licenses.

## NAudio 3.1.0

Copyright 2008-2026 Mark Heath. Distribuído sob MIT.
Código e licença: https://github.com/naudio/NAudio

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## FFmpeg

O build usado no desenvolvimento é chamado como programa separado, por pipe,
com suporte a libopus. A cópia de licença GPLv3 em tools/FFmpeg-LICENSE.txt refere-se
a esse build; confira a licença exata de qualquer outro executável obtido por você.
Distribuição original: https://www.gyan.dev/ffmpeg/builds/
Código-fonte: https://ffmpeg.org/download.html
Configuração exata: execute tools/ffmpeg.exe -version.

## Microsoft .NET

A publicação independente inclui o runtime .NET. Antes de redistribuir binários,
inclua também os termos e avisos de licença originais da versão correspondente
do runtime .NET e Windows Desktop. O repositório inicial distribui apenas fontes.
System.Numerics.Tensors 9.0.0: https://github.com/dotnet/runtime (MIT).
