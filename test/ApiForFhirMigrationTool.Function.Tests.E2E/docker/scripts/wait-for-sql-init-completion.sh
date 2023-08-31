#!/bin/bash

# Calls SQLCMD to verify that system and user databases return "0" which means all databases are in an "online" state,
# then run the configuration script (setup.sql)
# https://docs.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-databases-transact-sql?view=sql-server-2017 

TRIES=60
DBSTATUS=1
ERRCODE=1
i=0

while [[ $DBSTATUS -ne 0 ]] && [[ $i -lt $TRIES ]]; do
	i=$((i+1))
	DBSTATUS=$(/opt/mssql-tools/bin/sqlcmd -S mssql -h -1 -t 1 -U fhirAdmin -P $MSSQL_FHIRADMIN_PASSWORD -Q "SET NOCOUNT ON; Select COALESCE(SUM(state), 0) from sys.databases") || DBSTATUS=1
	
	sleep 5s
done

if [ $DBSTATUS -ne 0 ]; then 
	echo "SQL Server took more than $TRIES seconds to start up or one or more databases are not in an ONLINE state"
	exit 1
fi

echo "SQL Server container is up."
exit 0