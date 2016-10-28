-- TESTING if the order of execution will be OK. We assume Mytable already exists, because created in Rev001
INSERT INTO Mytable (GoodTable) VALUES ('TEST1');
INSERT INTO Mytable (GoodTable) VALUES ('TEST2');
INSERT INTO Mytable (GoodTable) VALUES ('TEST3');