CONFIG = ENV['CONFIG'] || 'Debug'

CORE_CSPROJ = 'Gyedo.Core/Gyedo.Core.csproj'
CSPROJ = 'Gyedo.Wpf/Gyedo.Wpf.csproj'
TEMPLATES_CSPROJ = 'Gyedo.Templates/Gyedo.Templates.csproj'
UNIT_TESTS = 'Gyedo.Wpf.Tests/Gyedo.Wpf.Tests.csproj'
CORE_TESTS = 'Gyedo.Core.Tests/Gyedo.Core.Tests.csproj'

TEMPLATES_DIR = 'Gyedo.Templates/templates'

desc "Build the project using the current CONFIG (or Debug)"
task :build do
  sh 'dotnet', 'build', '-c', CONFIG, CSPROJ
end

desc "Run unit tests using the current CONFIG (or Debug)"
task :test do
  sh 'dotnet', 'test', '-c', CONFIG, CORE_TESTS
  sh 'dotnet', 'test', '-c', CONFIG, UNIT_TESTS
end

desc "Create NuGet package"
task :package do
  sh 'dotnet', 'pack', '--no-build', '-c', CONFIG, CORE_CSPROJ, '-p:IncludeSymbols=true'
  sh 'dotnet', 'pack', '--no-build', '-c', CONFIG, CSPROJ, '-p:IncludeSymbols=true'
  sh 'dotnet', 'pack', '-c', CONFIG, TEMPLATES_CSPROJ
end

desc "Bump version number"
task :version, [:version] do |t, args|
  parts = args[:version].split('.')
  parts << '0' if parts.length == 3
  version = parts.join('.')

  [CORE_CSPROJ, CSPROJ, TEMPLATES_CSPROJ].each do |csproj|
    content = IO.read(csproj)
    content[/<VersionPrefix>(.+?)<\/VersionPrefix>/, 1] = version
    File.open(csproj, 'w'){ |f| f.write(content) }
  end

  Dir[File.join(TEMPLATES_DIR, '**/*.csproj')].each do |csproj|
    content = IO.read(csproj)
    content[/<PackageReference Include="Gyedo.Wpf" Version="(.+?)" \/>/, 1] = version
    File.open(csproj, 'w'){ |f| f.write(content) }
  end
end
