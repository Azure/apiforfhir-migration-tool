# Running E2E Tests

The E2E tests in this project rely on a working Migration Tool function endpoint. This can be set via setting the "TestMigrationFunctionUrl" in your environment. If this is not set, the E2E test assumes you will be using a local version of the migration tool that the testing framework will launch or is already available on port 7071. This is useful as you can launch another instance of your IDE and debug through the logic called by the E2E tests in your other IDE.

When running the local tests, there are a series of dependencies needed (API for FHIR, FHIR Service, Azure Storage) which can easily be run locally by running the docker compose file in the `docker` directory of this project. You can launch this by running the below from this directory.

```bash
docker-compose -f docker/docker-compose-e2e.yml up
```