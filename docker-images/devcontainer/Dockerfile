FROM ubuntu:20.04

ARG DEBIAN_FRONTEND=noninteractive

RUN apt update; \
	apt install -y git \
		wget \
		curl; \
	# Install Mono
	apt install gnupg ca-certificates; \
	apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF; \
	echo "deb https://download.mono-project.com/repo/ubuntu stable-focal main" | tee /etc/apt/sources.list.d/mono-official-stable.list; \
	apt install -y mono-complete; \
	# Install latest .NET Core 5.0
	wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb; \
	dpkg -i packages-microsoft-prod.deb; \
	apt update; \
	apt install -y apt-transport-https; \
	apt update; \
	apt install -y dotnet-sdk-5.0; \
	# Install latest .NET Core 3.1
	wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
	dpkg -i packages-microsoft-prod.deb && \
	apt update && \
	apt install -y apt-transport-https && \
	apt update && \
	apt install -y dotnet-sdk-3.1; \
	# install PowerShell
	apt update; \
	apt install -y powershell; \
	# add dev non-root user
	useradd --shell /bin/bash --create-home devcontainer;

USER devcontainer

# Built with ❤ by [Pipeline Foundation](https://pipeline.foundation)