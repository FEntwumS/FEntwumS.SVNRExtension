
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

-- Uncomment the following library declaration if using
-- arithmetic functions with Signed or Unsigned values
--use IEEE.NUMERIC_STD.ALL;

-- Uncomment the following library declaration if instantiating
-- any Xilinx leaf cells in this code.
--library UNISIM;
--use UNISIM.VComponents.all;

entity mux_2_n is
    Generic ( size : integer := 1);
    Port ( data1 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data0 : in STD_LOGIC_VECTOR (size-1 downto 0);
           result : out STD_LOGIC_VECTOR (size-1 downto 0);
           sel : in STD_LOGIC);
end mux_2_n;

architecture Behavioral of mux_2_n is

begin

    with sel select
        result  <=  data0 when '0',
                    data1 when '1',
                    (others => '0') when others;


end Behavioral;
