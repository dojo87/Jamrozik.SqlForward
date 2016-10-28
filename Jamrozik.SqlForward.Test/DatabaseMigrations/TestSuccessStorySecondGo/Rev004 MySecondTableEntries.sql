-- TESTING if the order of execution will be OK. We assume Mytable already exists, because created in Rev001
INSERT INTO MySecondTable (GoodTable) VALUES ('GOOD1');
INSERT INTO MySecondTable (GoodTable) VALUES ('GOOD2');
INSERT INTO MySecondTable (GoodTable) VALUES ('GOOD3');
INSERT INTO MySecondTable (GoodTable) VALUES ('GOOD4');