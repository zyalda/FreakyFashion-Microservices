FROM ://microsoft.com AS base
WORKDIR /home/site/wwwroot
EXPOSE 80

# .NET 10.0 SDK to compile code.
FROM ://microsoft.com AS build
WORKDIR /src

# Copy project files and run NuGet restore
COPY ["FreakyFashion-OrderService.csproj", "./"]
RUN dotnet restore "FreakyFashion-OrderService.csproj"

COPY . .
RUN dotnet build "FreakyFashion-OrderService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FreakyFashion-OrderService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot