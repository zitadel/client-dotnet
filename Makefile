# Self-contained SDK (re)generation for this client repo.
#
#   make generate   # regenerate from the OpenAPI spec, then prune orphans
#
# Regeneration overwrites every generator-owned file and then deletes any
# tracked file the generator no longer produces — EXCEPT the keep-list in
# .openapi-generator-ignore (bespoke auth, tests, identity, house tooling),
# which the generator skips and `prune` spares.

GENERATOR  ?= csharp-plus
IMAGE      ?= openapi-generator-plus:enhanced
SPEC_DIR   ?= ../sdk/spec
VERSION    ?= v4.11.0
NORMALIZER ?= NORMALIZER_CLASS=io.github.mridang.codegen.AdvancedOpenAPINormalizer,GARBAGE_COLLECT_COMPONENTS=1,STRIP_PARAMS=Connect-Protocol-Version|Connect-Timeout-Ms,CLEAN_EMPTY_REQUEST_BODIES=Tag,ONLY_ALLOW_JSON=1

REPO := $(notdir $(CURDIR))

# packageName "Zitadel.Client" expands (dot -> slash) to src/Zitadel/Client,
# but the SDK project (csproj, bespoke Auth, test reference) lives in the
# dot-named folder src/Zitadel.Client. After generation we relocate the
# generated tree into the dot-named folder and rewrite the FILES manifest so
# `prune` keeps matching the on-disk layout.
GEN_DIR := src/Zitadel/Client
PKG_DIR := src/Zitadel.Client

# The generator emits its spec-independent unit tests in a flat layout at the
# output root (Test/*.cs, xunit.runner.json, Zitadel.Client.Test.csproj). This
# repo keeps a nested test project (test/Zitadel.Client.Test) that also hosts
# the bespoke integration specs + transport test and a hand-maintained csproj.
# After generation we relocate the generator-owned unit tests + runner config
# into the nested project so the keep-listed csproj globs them, and drop the
# generated root csproj (the hand-maintained one wins).
TEST_GEN_DIR := Test
TEST_PKG_DIR := test/Zitadel.Client.Test

.PHONY: generate relocate prune build test lint format format-dotnet analyze docs clean

generate:
	docker run --rm \
	  -v "$(CURDIR):/sdk/out" \
	  -v "$(abspath $(SPEC_DIR)):/sdk/spec:ro" \
	  -v "$(CURDIR)/proc.yml:/sdk/proc.yml:ro" \
	  --user "$$(id -u):$$(id -g)" \
	  $(IMAGE) generate \
	    --input-spec=/sdk/spec/client/$(VERSION)/index.json \
	    --generator-name=$(GENERATOR) \
	    --output=/sdk/out \
	    --git-user-id=zitadel --git-repo-id=$(REPO) --git-host=github.com \
	    --config=/sdk/proc.yml \
	    --openapi-normalizer "$(NORMALIZER)"
	@$(MAKE) --no-print-directory relocate
	@$(MAKE) --no-print-directory prune
	@$(MAKE) --no-print-directory format

relocate:
	@test -d "$(GEN_DIR)" || { echo "relocate: $(GEN_DIR) absent, skipping"; exit 0; }
	@for sub in Api Models Errors; do \
	  if [ -d "$(GEN_DIR)/$$sub" ]; then rm -rf "$(PKG_DIR)/$$sub"; mv "$(GEN_DIR)/$$sub" "$(PKG_DIR)/$$sub"; fi; \
	done
	@if [ -d "$(GEN_DIR)/Auth" ]; then for f in "$(GEN_DIR)"/Auth/*.cs; do [ -e "$$f" ] && mv -f "$$f" "$(PKG_DIR)/Auth/$$(basename "$$f")"; done; fi
	@for f in "$(GEN_DIR)"/*.cs; do [ -e "$$f" ] && mv -f "$$f" "$(PKG_DIR)/$$(basename "$$f")"; done
	@# Drop the generated csproj (the hand-maintained one in $(PKG_DIR) wins).
	@rm -f "$(GEN_DIR)"/*.csproj
	@rm -rf src/Zitadel
	@# Relocate the generator-owned unit tests + runner config into the nested
	@# test project. NOTE: on a case-insensitive filesystem the generator's
	@# "Test/" dir is the same as this repo's "test/", so the emitted unit tests
	@# land directly under test/ alongside the nested Zitadel.Client.Test/ dir.
	@for f in "$(TEST_GEN_DIR)"/*.cs; do [ -e "$$f" ] && mv -f "$$f" "$(TEST_PKG_DIR)/$$(basename "$$f")"; done; true
	@if [ -f xunit.runner.json ]; then mv -f xunit.runner.json "$(TEST_PKG_DIR)/xunit.runner.json"; fi
	@# Drop the generated root test csproj (the hand-maintained nested one wins).
	@rm -f Zitadel.Client.Test.csproj
	@# Rewrite the FILES manifest so prune matches the relocated layout.
	@if [ -f .openapi-generator/FILES ]; then \
	  sed -e 's#^src/Zitadel/Client/[^/]*\.csproj$$##' \
	      -e 's#^src/Zitadel/Client/#src/Zitadel.Client/#' \
	      -e 's#^Zitadel\.Client\.Test\.csproj$$##' \
	      -e 's#^xunit\.runner\.json$$#$(TEST_PKG_DIR)/xunit.runner.json#' \
	      -e 's#^Test/#$(TEST_PKG_DIR)/#' \
	      .openapi-generator/FILES | sed '/^$$/d' > .openapi-generator/FILES.tmp; \
	  mv .openapi-generator/FILES.tmp .openapi-generator/FILES; \
	fi
	@echo "relocate: moved generated tree into $(PKG_DIR) and unit tests into $(TEST_PKG_DIR)"

prune:
	@test -f .openapi-generator/FILES || { echo "prune: no FILES manifest, skipping"; exit 0; }
	@tmp=$$(mktemp -d); \
	git ls-files | sort > "$$tmp/tracked"; \
	sed 's#^\./##' .openapi-generator/FILES | sort -u > "$$tmp/generated"; \
	awk 'NR==FNR { gen[tolower($$0)]=1; next } !(tolower($$0) in gen)' \
	    "$$tmp/generated" "$$tmp/tracked" > "$$tmp/orphans"; \
	git -c core.excludesFile="$(CURDIR)/.openapi-generator-ignore" \
	    check-ignore --no-index --stdin < "$$tmp/orphans" 2>/dev/null | sort -u > "$$tmp/keep" || true; \
	comm -23 "$$tmp/orphans" "$$tmp/keep" > "$$tmp/delete"; \
	echo "prune: deleting $$(wc -l < "$$tmp/delete" | tr -d ' ') orphaned files the generator no longer produces"; \
	xargs -r rm -f < "$$tmp/delete"; \
	rm -rf "$$tmp"

build:
	dotnet build

test:
	dotnet test --verbosity normal

lint:
	dotnet csharpier check .
	dotnet format --verify-no-changes --severity warn

format:
	dotnet csharpier format .
	dotnet format --severity warn

format-dotnet:
	dotnet format --severity warn

analyze:
	dotnet build --no-restore /warnaserror

docs:
	dotnet tool restore
	dotnet docfx metadata docfx.json --warningsAsErrors
	dotnet docfx build docfx.json --warningsAsErrors

clean:
	dotnet clean
