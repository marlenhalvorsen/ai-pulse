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

# Azure App Service persistent storage mounts at /home — DB lives there
RUN mkdir -p /home/data

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AiPulse.Api.dll"]
