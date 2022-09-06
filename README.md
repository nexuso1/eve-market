# Program usage
-------------
## Commands
```
authorize 
```
- Used to authoriza a character

```
logout 
```
- Removes the current character authorization


```
set region "{region_name}" 
```
- Sets the default region to the given region


```
set station "{station_name}"
```
- Sets the default station the the given station


```
orders "{type_name}" ["{region_name}"] [-a] [-de] [-d] [-v] [-p] 
```
- Displays current buy and sell orders for type {type_name} in
 region {region_name}, or in the default one if no region name
 is provided. 
- Sell orders are by default price ascending, buy orders price
 price descending.
- Additional options:
-a  : Sort type ascending
-de : Sort type descending
-d  : Sort by date
-v  : Sort by volume remaining
-p  : Sort by price

```
wallet
```
- Displays the current account balance of the authorized character
transactions
- Displays the transaction history of the authorized character

```
my_orders [-a] [-de] [-d] [-v] [-p]
```
- Displays the active market orders of the authorized character
- Additional options:
-a  : Sort type ascending
-de : Sort type descending
-d  : Sort by date
-v  : Sort by volume remaining
-p  : Sort by price

```
my_order_history [-a] [-de] [-d] [-v] [-p]
```
- Displays all completed market orders of the authorized character
- Additional options:
-a  : Sort type ascending
-de : Sort type descending
-d  : Sort by date
-v  : Sort by volume remaining
-p  : Sort by price

```
info "{type_name}"
```
- Displays information about the given type
history "{type_name}" ["{region_name}"]
- Displays price and volume history of type {type_name} in region
 {region_name}, or the default region if no region name specified.

```
assets
```
- Displays all assets of the authorized character
contracts page_number ["{region_name}"]
- Displays page page_number of public contracts (or all contracts 
 available to the authorized character) in the given region,
 or the default one if no region name is provided.

```
my_contracts
```
- Displays all contracts of the authorized character.
  
```
exit
```
- Exits the program.

```
help
```
- Displays the help