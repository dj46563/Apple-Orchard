<?php include "../inc/dbinfo.inc"; ?>
<?php 
    http_response_code(400);

    $con = mysqli_connect(DB_SERVER, DB_USERNAME, DB_PASSWORD, DB_DATABASE);

    //Check that connection worked
    if (mysqli_connect_errno()) {
        die("1\tConnect error: " . mysqli_connect_error());
    }

    $hash = $_POST["hash"];

    $hashQuery = sprintf("SELECT id, apples, username FROM players WHERE hash='%s'", $hash);
    $hashCheck = mysqli_query($con, $hashQuery) or die ("2\tHash query failed");

    if (mysqli_num_rows($hashCheck) == 0){
        echo "3\Hash does not exist";
        exit();
    }

    $playerInfo = mysqli_fetch_assoc($hashCheck);
    
    http_response_code(200);

    $response->id = $playerInfo["id"];
    $response->username = $playerInfo["username"];;
    $response->apples = $playerInfo["apples"];;
    echo(json_encode($response));
?>