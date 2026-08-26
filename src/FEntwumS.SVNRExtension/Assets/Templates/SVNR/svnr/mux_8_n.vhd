

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

-- Uncomment the following library declaration if using
-- arithmetic functions with Signed or Unsigned values
--use IEEE.NUMERIC_STD.ALL;

-- Uncomment the following library declaration if instantiating
-- any Xilinx leaf cells in this code.
--library UNISIM;
--use UNISIM.VComponents.all;

entity mux_8_n is
    Generic ( size : integer := 1);
    Port ( data7 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data6 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data5 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data4 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data3 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data2 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data1 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data0 : in STD_LOGIC_VECTOR (size-1 downto 0);
           result : out STD_LOGIC_VECTOR (size-1 downto 0);
           sel : in STD_LOGIC_VECTOR (2 downto 0));
end mux_8_n;

architecture Behavioral of mux_8_n is

begin

    with sel select
        result  <=  data0 when "000",
                    data1 when "001",
                    data2 when "010",
                    data3 when "011",
                    data4 when "100",
                    data5 when "101",
                    data6 when "110",
                    data7 when "111",
                    (others => '0') when others;  


end Behavioral;
