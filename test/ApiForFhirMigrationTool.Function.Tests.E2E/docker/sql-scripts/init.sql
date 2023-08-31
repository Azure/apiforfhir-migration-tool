-- Create the database
CREATE DATABASE fhirR4 COLLATE SQL_Latin1_General_CP1_CI_AS;
GO

PRINT 'Database fhirR4 created successfully.'

-- Switch to the database
USE fhirR4;
GO

-- Create a new login for the user
CREATE LOGIN fhirAdmin WITH PASSWORD = 'YourStrongPassword1!';
GO

PRINT 'Login fhirAdmin created successfully.'

-- Create a user for the login
CREATE USER fhirAdmin FOR LOGIN fhirAdmin;
GO

-- Add the user to the db_owner role, granting full rights over the database
ALTER ROLE db_owner ADD MEMBER fhirAdmin;
GO

PRINT 'User fhirAdmin created successfully.'