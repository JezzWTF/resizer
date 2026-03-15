PROJ := src/BatchResizer/BatchResizer.csproj
PUBLISH_DIR := ./publish

.PHONY: all build release publish run clean restore icon

all: build

## Build (debug)
build:
	dotnet build $(PROJ)

## Build (release)
release:
	dotnet build $(PROJ) -c Release

## Restore NuGet packages
restore:
	dotnet restore $(PROJ)

## Publish self-contained single-file exe
publish:
	dotnet publish $(PROJ) \
		-c Release \
		-r win-x64 \
		--self-contained true \
		-p:PublishSingleFile=true \
		-p:EnableCompressionInSingleFile=true \
		-o $(PUBLISH_DIR)
	@echo Published to $(PUBLISH_DIR)

## Run (debug)
run:
	dotnet run --project $(PROJ)

## Regenerate icon.ico from icon.svg
icon:
	magick -background none icon.svg \
		-define icon:auto-resize="256,128,64,48,32,16" \
		src/BatchResizer/icon.ico
	@echo Icon regenerated.

## Remove build artifacts
clean:
	dotnet clean $(PROJ)
	rm -rf $(PUBLISH_DIR)
