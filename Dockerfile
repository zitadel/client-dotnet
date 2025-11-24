FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

COPY . .

RUN dotnet tool install -g csharprepl
ENV PATH="${PATH}:/root/.dotnet/tools"

RUN dotnet build

CMD ["csharprepl", "-r", "src/Zitadel.Client/bin/Debug/net10.0/Zitadel.Client.dll"]
