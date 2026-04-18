FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AiPulse.Domain/ AiPulse.Domain/
COPY AiPulse.Application/ AiPulse.Application/
COPY AiPulse.Infrastructure/ AiPulse.Infrastructure/
COPY AiPulse.Api/ AiPulse.Api/

RUN dotnet publish AiPulse.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

# Render injects PORT at runtime; default to 10000 (Render standard)
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "AiPulse.Api.dll"]
